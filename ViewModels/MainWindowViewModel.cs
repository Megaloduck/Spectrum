using Avalonia;
using Avalonia.Animation;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spectrum.Models;
using Spectrum.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Spectrum.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public ObservableCollection<ColorSwatch> Palette { get; } = new();

        /// <summary>The sidebar palette library (§1): list, search, pin, versions.</summary>
        public PaletteLibraryViewModel Library { get; }

        private string? _activePaletteId;
        private bool _isLoadingBoard;

        /// <summary>The library id of the palette currently loaded on the board —
        /// read by the workspace ViewModels that operate on it.</summary>
        internal string? ActivePaletteId => _activePaletteId;

        // ---------------- Workspaces (§7 desktop UX) ----------------
        // Each workspace owns its own state and commands; this shell composes
        // them (views bind e.g. {Binding Studio.Hue}) while the shared board
        // state — palette, history, library, status — stays here.

        /// <summary>Ctrl+1 · Studio: color panel, eyedroppers, generators.</summary>
        public StudioWorkspaceViewModel Studio { get; }

        /// <summary>Ctrl+2 · Preview: mockup, variants, comparison.</summary>
        public PreviewWorkspaceViewModel Preview { get; }

        /// <summary>Ctrl+3 · Analyze: color science, contrast, matrix.</summary>
        public AnalyzeWorkspaceViewModel Analyze { get; }

        /// <summary>Ctrl+4 · Export: formats, preview, file output.</summary>
        public ExportWorkspaceViewModel Export { get; }

        public Array ColorBlindModes { get; } = Enum.GetValues(typeof(ColorBlindMode));

        /// <summary>Notations offered by the footer copy-format picker (§2/§10).</summary>
        public Array CopyFormats { get; } = Enum.GetValues(typeof(CopyFormat));

        [ObservableProperty]
        private CopyFormat _selectedCopyFormat = CopyFormat.Hex;

        /// <summary>The swatch most recently clicked on the board (§8 zoom, Delete/Ctrl+D shortcuts).</summary>
        [ObservableProperty]
        private ColorSwatch? _selectedSwatch;

        // ---------------- §8 Preview & visualization ----------------

        public Array ViewModes { get; } = Enum.GetValues(typeof(ViewMode));

        [ObservableProperty]
        private ViewMode _viewMode = ViewMode.Row;

        /// <summary>Raised when the board layout must switch templates (handled in code-behind).</summary>
        public event Action? ViewModeRequested;

        partial void OnViewModeChanged(ViewMode value) => ViewModeRequested?.Invoke();

        [ObservableProperty]
        private ColorBlindMode _selectedColorBlindMode = ColorBlindMode.None;

        [ObservableProperty]
        private bool _showContrastOverlay;

        partial void OnShowContrastOverlayChanged(bool value)
        {
            foreach (var swatch in Palette)
                swatch.ShowContrastOverlay = value;
        }

        // ---------------- Active workspace (§7 desktop UX) ----------------

        [ObservableProperty]
        private WorkspaceMode _workspaceMode = WorkspaceMode.Studio;

        partial void OnWorkspaceModeChanged(WorkspaceMode value)
        {
            // Each workspace is a dedicated view that the shell shows/hides from
            // these flags, so switching only has to refresh them.
            OnPropertyChanged(nameof(IsStudioMode));
            OnPropertyChanged(nameof(IsPreviewMode));
            OnPropertyChanged(nameof(IsAnalyzeMode));
            OnPropertyChanged(nameof(IsExportMode));
        }

        public bool IsStudioMode => WorkspaceMode == WorkspaceMode.Studio;
        public bool IsPreviewMode => WorkspaceMode == WorkspaceMode.Preview;
        public bool IsAnalyzeMode => WorkspaceMode == WorkspaceMode.Analyze;
        public bool IsExportMode => WorkspaceMode == WorkspaceMode.Export;

        /// <summary>App-bar segment switch + Ctrl+1..4 + command palette.</summary>
        [RelayCommand]
        private void SetWorkspace(WorkspaceMode mode)
        {
            if (WorkspaceMode == mode) return;
            WorkspaceMode = mode;

            StatusMessage = mode switch
            {
                WorkspaceMode.Studio => "Studio — generate and edit the palette.",
                WorkspaceMode.Preview => "Preview — mockup, variants and side-by-side compare.",
                WorkspaceMode.Analyze => "Analyze — contrast, conversions and color-vision simulation.",
                _ => "Export — send the palette out, or bring files in.",
            };
        }


        private string _statusMessage = "Ready to Inspire.";

        /// <summary>
        /// Public status-message setter. Every assignment updates the status bar
        /// (bound from StatusBarView.axaml) and the board's status strip, so the
        /// user sees the result of an action even while scrolled away.
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => _statusMessage = value;
        }

        [ObservableProperty]
        private int _paletteCount;

        public string PaletteCountLabel => PaletteCount == 1 ? "1 color" : $"{PaletteCount} colors";

        partial void OnPaletteCountChanged(int value) => OnPropertyChanged(nameof(PaletteCountLabel));



        /// <summary>Returns true while a destructive action can be undone.</summary>
        public bool CanUndoToast => _undoStack.Count > 0 || PaletteLibraryService.CanUndoLastDelete;

        // ---- Undo/redo history (covers add/remove/clear/generate/extract/load —
        // ---- see PushHistory call sites. In-place edits like renaming or
        // ---- toggling a lock aren't tracked.)
        private readonly Stack<List<SwatchSnapshot>> _undoStack = new();
        private readonly Stack<List<SwatchSnapshot>> _redoStack = new();
        private bool _isRestoringHistory;

        private readonly record struct SwatchSnapshot(Color Color, string Name, bool IsLocked);

        public MainWindowViewModel()
        {
            // Keep the header's "N colors" pill in sync no matter what
            // caused the palette to change (add, remove, clear, generate...),
            // and mirror every edit into the library DTO so autosave can
            // persist it (§1 auto-save / dirty state).
            Palette.CollectionChanged += (_, _) =>
            {
                PaletteCount = Palette.Count;
                Preview?.RaiseMockupPropertiesChanged();
                if (!_isLoadingBoard && !_isRestoringHistory) SyncActivePalette();
            };

            Library = new PaletteLibraryViewModel();
            Library.ActivePaletteChanged += OnActivePaletteChanged;

            // The workspace ViewModels own their own state and commands; they
            // reach the shared board through the shell they're built with.
            Studio = new StudioWorkspaceViewModel(this);
            Preview = new PreviewWorkspaceViewModel(this);
            Analyze = new AnalyzeWorkspaceViewModel(this);
            Export = new ExportWorkspaceViewModel(this);

            // Analyze's conversion / contrast readouts derive from Studio's
            // base color, so they refresh whenever it changes.
            Studio.BaseColorChanged += Analyze.RefreshForBaseColor;

            // System-wide hotkey for the screen eyedropper (§3). The binding
            // itself is registered by SettingsService at startup so the user's
            // configured combination is honoured from the first launch.
            GlobalHotkeyService.HotkeyRaised += OnGlobalHotkeyRaised;

            // §10 defaults + §3 picked-color history.
            ApplyFormatDefaults();
            Studio.RestoreRecentPicks();

            if (PaletteLibraryService.Library.Palettes.Count == 0)
            {
                // Brand-new library: seed a first palette (selecting it loads the board).
                Library.CreateDefaultPalette();
            }
            else
            {
                var activeId = PaletteLibraryService.Library.ActivePaletteId;
                var active = PaletteLibraryService.Library.Palettes.FirstOrDefault(p => p.Id == activeId)
                             ?? PaletteLibraryService.Library.Palettes[0];
                Library.SelectPalette(active);
            }
        }

        /// <summary>§10 defaults, read at startup and again whenever Settings changes.</summary>
        public void ApplyFormatDefaults()
        {
            if (Enum.TryParse<CopyFormat>(SettingsService.Settings.DefaultCopyFormat, out var copy))
                SelectedCopyFormat = copy;
            if (Enum.TryParse<ExportFormat>(SettingsService.Settings.DefaultExportFormat, out var export))
                Export.SelectedExportFormat = export;
        }

        /// <summary>§10 Settings dialog (Ctrl+,).</summary>
        [RelayCommand]
        private async Task OpenSettingsAsync()
        {
            if (GetOwnerWindow() is not { } owner) return;

            var window = new Views.SettingsWindow();
            if (Library is not null) window.AttachLibrary(Library);
            await window.ShowDialog(owner);
            StatusMessage = "Settings saved.";
        }

        /// <summary>§7 "Command palette (Ctrl+K)": fuzzy-searchable list of every command.</summary>
        private async void OpenCommandPalette()
        {
            if (GetOwnerWindow() is not { } owner) return;
            var palette = new Views.CommandPaletteWindow(this);
            await palette.ShowDialog(owner);
        }

        // Panel collapse toggles (§7 "Resizable / collapsible panels"). Plain
        // bindable flags: the shell binds the library column to LibraryVisible and
        // every workspace binds its own dock to InspectorVisible, so no view has to
        // subscribe to an event to know when a panel closed.
        [ObservableProperty]
        private bool _libraryVisible = true;

        [ObservableProperty]
        private bool _inspectorVisible = true;

        [RelayCommand]
        private void ToggleLibraryPanel() => LibraryVisible = !LibraryVisible;

        [RelayCommand]
        private void ToggleInspector() => InspectorVisible = !InspectorVisible;

        /// <summary>§7 "Multi-window support": detach the current palette into its own window.</summary>
        [RelayCommand]
        private void DetachWindow()
        {
            if (GetOwnerWindow() is not { } owner) return;

            var window = new Views.MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
            window.Show(owner);
            StatusMessage = "Palette detached into a new window.";
        }

        /// <summary>§3 "Global hotkey to trigger picker" — Ctrl+Alt+P by default (rebindable in Settings).</summary>
        private void OnGlobalHotkeyRaised()
        {
            if (Studio.PickFromScreenCommand.CanExecute(null))
                Studio.PickFromScreenCommand.Execute(null);
        }

        internal static Avalonia.Controls.Window? GetOwnerWindow()
        {
            if (Application.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }

            return null;
        }

        /// <summary>Called by the window on close so the library can detach its service hooks.</summary>
        public void Detach()
        {
            Library.Detach();
            GlobalHotkeyService.HotkeyRaised -= OnGlobalHotkeyRaised;
        }

        // ---------------- Library ↔ board synchronization ----------------

        /// <summary>Loads a library palette into the board (and clears undo history).</summary>
        private void OnActivePaletteChanged(PaletteDto? dto)
        {
            _isLoadingBoard = true;
            try
            {
                Palette.Clear();
                _activePaletteId = dto?.Id;

                if (dto is not null)
                {
                    foreach (var s in dto.Swatches)
                    {
                        var swatch = CreateSwatch(Color.FromArgb(s.A, s.R, s.G, s.B), s.Name);
                        swatch.IsLocked = s.IsLocked;
                        Palette.Add(swatch);
                    }
                }
            }
            finally
            {
                _isLoadingBoard = false;
            }

            ClearHistory();
            StatusMessage = dto is null ? "No palette selected." : $"Opened \"{dto.Name}\".";
        }

        /// <summary>Writes the board back into the active palette's DTO and queues an autosave.</summary>
        private void SyncActivePalette()
        {
            if (_isLoadingBoard || _activePaletteId is null) return;
            var dto = PaletteLibraryService.FindPalette(_activePaletteId);
            if (dto is null) return;

            dto.Swatches = Palette
                .Select(s => new PaletteSwatchDto(s.Name, s.Color.A, s.Color.R, s.Color.G, s.Color.B, s.IsLocked))
                .ToList();

            PaletteLibraryService.MarkDirty(dto);
            Library.RefreshPreview(dto);
        }

        private void OnSwatchPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoadingBoard) return;
            if (e.PropertyName is nameof(ColorSwatch.Color) or nameof(ColorSwatch.Name) or nameof(ColorSwatch.IsLocked))
            {
                SyncActivePalette();
                Preview.RaiseMockupPropertiesChanged();
            }
        }

        partial void OnSelectedColorBlindModeChanged(ColorBlindMode value)
        {
            foreach (var swatch in Palette)
            {
                swatch.ColorBlindMode = value;
            }
        }

        // Wires each swatch's card buttons back to this ViewModel's commands
        // so the UI never needs a reference back up to the parent DataContext.
        internal ColorSwatch CreateSwatch(Color color, string name)
        {
            var swatch = new ColorSwatch(color, name)
            {
                ColorBlindMode = SelectedColorBlindMode,
                ShowContrastOverlay = ShowContrastOverlay,
                CopyCommand = CopyHexCommand,
                RemoveCommand = RemoveSwatchCommand,
                MoveUpCommand = MoveSwatchUpCommand,
                MoveDownCommand = MoveSwatchDownCommand,
                EditColorCommand = EditSwatchColorCommand,
                DuplicateCommand = DuplicateSwatchCommand
            };

            swatch.PropertyChanged += OnSwatchPropertyChanged;
            return swatch;
        }

        // ---------------- Undo / redo ----------------

        private List<SwatchSnapshot> CaptureSnapshot() =>
            Palette.Select(s => new SwatchSnapshot(s.Color, s.Name, s.IsLocked)).ToList();

        internal void PushHistory()
        {
            if (_isRestoringHistory) return;
            _undoStack.Push(CaptureSnapshot());
            _redoStack.Clear();
            NotifyHistoryChanged();
        }

        private void RestoreSnapshot(List<SwatchSnapshot> snapshot)
        {
            _isRestoringHistory = true;
            _isLoadingBoard = true;
            Palette.Clear();
            foreach (var s in snapshot)
            {
                var swatch = CreateSwatch(s.Color, s.Name);
                swatch.IsLocked = s.IsLocked;
                Palette.Add(swatch);
            }
            _isRestoringHistory = false;
            _isLoadingBoard = false;
            SyncActivePalette();
        }

        private void ClearHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            NotifyHistoryChanged();
        }

        private void NotifyHistoryChanged()
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }

        private bool CanUndo() => _undoStack.Count > 0;
        private bool CanRedo() => _redoStack.Count > 0;

        [RelayCommand(CanExecute = nameof(CanUndo))]
        private void Undo()
        {
            if (_undoStack.Count == 0) return;
            _redoStack.Push(CaptureSnapshot());
            RestoreSnapshot(_undoStack.Pop());
            StatusMessage = "Undid last change.";
            NotifyHistoryChanged();
        }

        [RelayCommand(CanExecute = nameof(CanRedo))]
        private void Redo()
        {
            if (_redoStack.Count == 0) return;
            _undoStack.Push(CaptureSnapshot());
            RestoreSnapshot(_redoStack.Pop());
            StatusMessage = "Redid last change.";
            NotifyHistoryChanged();
        }

        // ---------------- Palette editing ----------------

        [RelayCommand]
        private void RemoveSwatch(ColorSwatch? swatch)
        {
            if (swatch is null) return;
            PushHistory();
            Palette.Remove(swatch);
            StatusMessage = "Swatch removed.";
        }

        [RelayCommand]
        private void ClearPalette()
        {
            PushHistory();
            Palette.Clear();
            StatusMessage = "Palette cleared.";
        }

        [RelayCommand]
        private void MoveSwatchUp(ColorSwatch? swatch)
        {
            if (swatch is null) return;
            var index = Palette.IndexOf(swatch);
            if (index > 0) Palette.Move(index, index - 1);
        }

        [RelayCommand]
        private void MoveSwatchDown(ColorSwatch? swatch)
        {
            if (swatch is null) return;
            var index = Palette.IndexOf(swatch);
            if (index >= 0 && index < Palette.Count - 1) Palette.Move(index, index + 1);
        }

        [RelayCommand]
        private void ToggleTheme()
        {
            var app = Application.Current;
            if (app is null) return;

            app.RequestedThemeVariant = app.ActualThemeVariant == ThemeVariant.Dark
                ? ThemeVariant.Light
                : ThemeVariant.Dark;
        }

        [RelayCommand]
        private async Task CopyHexAsync(ColorSwatch? swatch)
        {
            if (swatch is null) return;
            var text = ColorFormatService.Format(swatch.Color, SelectedCopyFormat);
            await ClipboardHelper.SetTextAsync(text);
            StatusMessage = $"Copied {text} ({SelectedCopyFormat}) to clipboard.";
        }

        /// <summary>§2 "Duplicate individual swatch".</summary>
        [RelayCommand]
        private void DuplicateSwatch(ColorSwatch? swatch)
        {
            if (swatch is null) return;
            PushHistory();

            var index = Palette.IndexOf(swatch);
            if (index < 0) index = Palette.Count - 1;

            var copy = CreateSwatch(swatch.Color, swatch.Name + " copy");
            copy.IsLocked = swatch.IsLocked;
            Palette.Insert(Math.Clamp(index + 1, 0, Palette.Count), copy);
            StatusMessage = $"Duplicated \"{swatch.Name}\".";
        }

        /// <summary>Opens the spectrum dialog and applies the result to a swatch card.</summary>
        [RelayCommand]
        private async Task EditSwatchColorAsync(ColorSwatch? swatch)
        {
            if (swatch is null) return;

            var picked = await PickColorAsync(swatch.Color);
            if (picked is null)
            {
                StatusMessage = "Color pick cancelled.";
                return;
            }

            PushHistory();
            swatch.Color = picked.Value;
            swatch.Name = ColorNamingService.GetClosestName(picked.Value);
            StatusMessage = $"Updated swatch to {swatch.Name}.";
        }

        /// <summary>Opens the spectrum dialog; shared by Studio's base-color picker,
        /// the swatch card editor and Analyze's contrast background picker.</summary>
        internal static async Task<Color?> PickColorAsync(Color initial)
        {
            if (Application.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is { } owner)
            {
                var dialog = new Views.ColorPickerDialog(initial);
                return await dialog.ShowDialog<Color?>(owner);
            }

            return null;
        }

        [RelayCommand]
        private async Task CopyAllHexAsync()
        {
            var text = PaletteExportService.Export(Palette, ExportFormat.PlainText);
            await ClipboardHelper.SetTextAsync(text);
            StatusMessage = "Copied full palette to clipboard.";
        }

        // ---------------- Palette persistence ----------------

        [RelayCommand]
        private async Task SavePaletteAsync()
        {
            var suggestedName = $"palette-{DateTime.Now:yyyyMMdd-HHmmss}";
            var path = await PaletteFileService.PickSaveFileAsync(suggestedName);
            if (string.IsNullOrEmpty(path))
            {
                StatusMessage = "Save cancelled.";
                return;
            }

            var dtos = Palette
                .Select(s => new PaletteSwatchDto(s.Name, s.Color.A, s.Color.R, s.Color.G, s.Color.B, s.IsLocked))
                .ToList();

            try
            {
                var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(path, json);
                StatusMessage = $"Palette saved to {Path.GetFileName(path)}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Couldn't save palette: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task LoadPaletteAsync()
        {
            var path = await PaletteFileService.PickOpenFileAsync();
            if (string.IsNullOrEmpty(path))
            {
                StatusMessage = "Load cancelled.";
                return;
            }

            List<PaletteSwatchDto>? dtos;
            try
            {
                var json = await File.ReadAllTextAsync(path);
                dtos = JsonSerializer.Deserialize<List<PaletteSwatchDto>>(json);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Couldn't load palette: {ex.Message}";
                return;
            }

            if (dtos is null)
            {
                StatusMessage = "That file didn't contain a usable palette.";
                return;
            }

            PushHistory();
            Palette.Clear();

            foreach (var dto in dtos)
            {
                var color = Color.FromArgb(dto.A, dto.R, dto.G, dto.B);
                var swatch = CreateSwatch(color, dto.Name);
                swatch.IsLocked = dto.IsLocked;
                Palette.Add(swatch);
            }

            StatusMessage = $"Loaded {Palette.Count} color{(Palette.Count == 1 ? "" : "s")} from {Path.GetFileName(path)}.";
        }
    }
}
