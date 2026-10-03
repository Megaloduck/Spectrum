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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Spectrum.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private const int DefaultPaletteSize = 5;

        public ObservableCollection<ColorSwatch> Palette { get; } = new();

        /// <summary>The sidebar palette library (§1): list, search, pin, versions.</summary>
        public PaletteLibraryViewModel Library { get; }

        private string? _activePaletteId;
        private bool _isLoadingBoard;

        public Array HarmonyTypes { get; } = Enum.GetValues(typeof(HarmonyType));
        public Array ExportFormats { get; } = Enum.GetValues(typeof(ExportFormat));
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
        private bool _showContrastOverlay;

        partial void OnShowContrastOverlayChanged(bool value)
        {
            foreach (var swatch in Palette)
                swatch.ShowContrastOverlay = value;
        }

        // ---------------- Workspaces (§7 desktop UX) ----------------

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

        // Live UI mockup brushes, derived from the current palette.
        private Color PaletteAt(int index, Color fallback) =>
            Palette.Count == 0
                ? fallback
                : Palette[Math.Clamp(index, 0, Palette.Count - 1)].Color;

        private Color MockupBackground => ColorMathService.Mix(PaletteAt(-1, Colors.White), Colors.White, 0.92);
        private Color MockupSurface => ColorMathService.Mix(PaletteAt(-1, Colors.White), Colors.White, 0.98);

        public IBrush MockupPrimaryBrush => new SolidColorBrush(PaletteAt(0, Colors.Gray));
        public IBrush MockupPrimaryForegroundBrush => new SolidColorBrush(ReadableOn(PaletteAt(0, Colors.Gray)));
        public IBrush MockupAccentBrush => new SolidColorBrush(PaletteAt(-1, Colors.Gray));
        public IBrush MockupAccentForegroundBrush => new SolidColorBrush(ReadableOn(PaletteAt(-1, Colors.Gray)));
        public IBrush MockupBackgroundBrush => new SolidColorBrush(MockupBackground);
        public IBrush MockupSurfaceBrush => new SolidColorBrush(MockupSurface);
        public IBrush MockupTextBrush => new SolidColorBrush(BestContrastText());

        private Color BestContrastText()
        {
            if (Palette.Count == 0) return Color.FromRgb(0x1E, 0x20, 0x25);

            return Palette
                .Select(s => s.Color)
                .OrderByDescending(c => ContrastService.ContrastRatio(c, MockupBackground))
                .First();
        }

        private static Color ReadableOn(Color color) =>
            (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0 > 0.6
                ? Color.FromRgb(0x1E, 0x20, 0x25)
                : Colors.White;

        private void RaiseMockupPropertiesChanged()
        {
            OnPropertyChanged(nameof(MockupPrimaryBrush));
            OnPropertyChanged(nameof(MockupPrimaryForegroundBrush));
            OnPropertyChanged(nameof(MockupAccentBrush));
            OnPropertyChanged(nameof(MockupAccentForegroundBrush));
            OnPropertyChanged(nameof(MockupBackgroundBrush));
            OnPropertyChanged(nameof(MockupSurfaceBrush));
            OnPropertyChanged(nameof(MockupTextBrush));
        }

        /// <summary>§8 "Dark / light mode variant generator": derives a lighter or darker
        /// sibling palette from the current one and opens it.</summary>
        [RelayCommand]
        private void GenerateLightVariant() => GenerateVariant(light: true);

        [RelayCommand]
        private void GenerateDarkVariant() => GenerateVariant(light: false);

        private void GenerateVariant(bool light)
        {
            if (Palette.Count == 0)
            {
                StatusMessage = "Add some colors before generating a variant.";
                return;
            }

            var baseName = PaletteLibraryService.FindPalette(_activePaletteId)?.Name ?? "Palette";
            var suffix = light ? "Light" : "Dark";
            var swatches = Palette.Select(s =>
            {
                var derived = light
                    ? ColorMathService.Mix(s.Color, Colors.White, 0.30)
                    : ColorMathService.Mix(s.Color, Colors.Black, 0.35);
                return new PaletteSwatchDto(
                    light ? $"{s.Name} tint" : $"{s.Name} shade",
                    derived.A, derived.R, derived.G, derived.B, s.IsLocked);
            }).ToList();

            var dto = PaletteLibraryService.AddPalette($"{baseName} ({suffix})", swatches);
            Library.AddAndSelect(dto);
            StatusMessage = $"Generated {suffix.ToLowerInvariant()} mode variant with {swatches.Count} colors.";
        }

        /// <summary>§8 "Side-by-side palette comparison".</summary>
        [RelayCommand]
        private async Task ComparePalettesAsync()
        {
            if (GetOwnerWindow() is not { } owner) return;

            var current = PaletteLibraryService.FindPalette(_activePaletteId)
                          ?? PaletteLibraryService.Library.Palettes.FirstOrDefault();

            var window = new Views.CompareWindow(current);
            await window.ShowDialog(owner);
        }

        /// <summary>Prevents two windows' hotkey handlers opening two pickers at once.</summary>
        private static bool ScreenPickerOpen;

        // ---- Base color, expressed as Hue/Saturation/Lightness + a
        // ---- Temperature tint applied on top, matching the "COLOR PANEL" sliders.
        [ObservableProperty]
        private double _hue = 9;

        [ObservableProperty]
        private double _saturation = 100;

        [ObservableProperty]
        private double _lightness = 64;

        [ObservableProperty]
        private double _temperature; // -100 (cool) .. 100 (warm), 0 = neutral

        [ObservableProperty]
        private double _alpha = 100; // 0 (transparent) .. 100 (opaque), applied to BaseColor

        [ObservableProperty]
        private HarmonyType _selectedHarmony = HarmonyType.Random;

        [ObservableProperty]
        private ColorBlindMode _selectedColorBlindMode = ColorBlindMode.None;

        [ObservableProperty]
        private ExportFormat _selectedExportFormat = ExportFormat.Json;

        [ObservableProperty]
        private string _exportPreview = string.Empty;

        [ObservableProperty]
        private string _statusMessage = "Ready to Inspire.";

        [ObservableProperty]
        private int _paletteCount;

        public string PaletteCountLabel => PaletteCount == 1 ? "1 color" : $"{PaletteCount} colors";

        partial void OnPaletteCountChanged(int value) => OnPropertyChanged(nameof(PaletteCountLabel));

        public Color BaseColor
        {
            get
            {
                var hslColor = ColorHarmonyService.FromHsl(Hue, Saturation / 100.0, Lightness / 100.0);
                var tinted = ApplyTemperature(hslColor, Temperature);
                return Color.FromArgb(ToByte(Alpha / 100.0 * 255.0), tinted.R, tinted.G, tinted.B);
            }
        }

        public IBrush BaseColorBrush => new SolidColorBrush(BaseColor);

        public string BaseColorName => ColorNamingService.GetClosestName(BaseColor);

        public IBrush BaseForegroundBrush =>
            (0.2126 * BaseColor.R + 0.7152 * BaseColor.G + 0.0722 * BaseColor.B) / 255.0 > 0.6
                ? new SolidColorBrush(Color.FromRgb(0x1E, 0x20, 0x25))
                : Brushes.White;

        public string BaseHex
        {
            get => $"#{BaseColor.R:X2}{BaseColor.G:X2}{BaseColor.B:X2}";
            set
            {
                if (!TryParseHex(value, out var color)) return;

                var (h, s, l) = ColorHarmonyService.ToHsl(color);
                Hue = h;
                Saturation = s * 100.0;
                Lightness = l * 100.0;
                Temperature = 0;
            }
        }

        // Percentage label for the Opacity slider's caption (e.g. "72%").
        public string AlphaLabel => $"{Alpha:N0}%";

            // ---- Gradient track backgrounds for the four sliders. Hue,
            // ---- Brightness and Temperature are fixed; Saturation's end color
            // ---- depends on the current Hue, so it's recomputed whenever that changes.
            public IBrush HueTrackBrush { get; } = CreateHorizontalGradient(
                (Color.FromRgb(255, 0, 0), 0.0),
                (Color.FromRgb(255, 255, 0), 1.0 / 6),
                (Color.FromRgb(0, 255, 0), 2.0 / 6),
                (Color.FromRgb(0, 255, 255), 3.0 / 6),
                (Color.FromRgb(0, 0, 255), 4.0 / 6),
                (Color.FromRgb(255, 0, 255), 5.0 / 6),
                (Color.FromRgb(255, 0, 0), 1.0));

            public IBrush SaturationTrackBrush => CreateHorizontalGradient(
                (Colors.White, 0.0),
                (ColorHarmonyService.FromHsl(Hue, 1.0, 0.5), 1.0));

            public IBrush BrightnessTrackBrush { get; } = CreateHorizontalGradient(
                (Colors.Black, 0.0),
                (Colors.White, 1.0));

            public IBrush TemperatureTrackBrush { get; } = CreateHorizontalGradient(
                (Color.FromRgb(0x4A, 0x90, 0xE2), 0.0),
                (Color.FromRgb(0xF5, 0xA6, 0x23), 1.0));

            // Unlike the other tracks, this one depends on the live base
            // color (so the gradient always fades *that* hue from
            // transparent to opaque) rather than a fixed set of stops, so
            // it's a plain computed property kept in sync via
            // RaiseColorPropertiesChanged instead of a field initializer.
            public IBrush AlphaTrackBrush => CreateHorizontalGradient(
                (Color.FromArgb(0, BaseColor.R, BaseColor.G, BaseColor.B), 0.0),
                (Color.FromArgb(255, BaseColor.R, BaseColor.G, BaseColor.B), 1.0));

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
                RaiseMockupPropertiesChanged();
                if (!_isLoadingBoard && !_isRestoringHistory) SyncActivePalette();
            };

            Library = new PaletteLibraryViewModel();
            Library.ActivePaletteChanged += OnActivePaletteChanged;

            // System-wide hotkey for the screen eyedropper (§3). The binding
            // itself is registered by SettingsService at startup so the user's
            // configured combination is honoured from the first launch.
            GlobalHotkeyService.HotkeyRaised += OnGlobalHotkeyRaised;

            // §10 defaults + §3 picked-color history.
            ApplyFormatDefaults();
            RestoreRecentPicks();

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
                SelectedExportFormat = export;
        }

        private void RestoreRecentPicks()
        {
            foreach (var hex in SettingsService.Settings.RecentPicks)
            {
                if (RecentPicks.Count >= MaxRecentPicks) break;
                if (ColorMathService.TryParseHex(hex, out var color))
                    RecentPicks.Add(new RecentPick(color));
            }
        }

        private void PersistRecentPicks()
        {
            SettingsService.Settings.RecentPicks = RecentPicks.Select(p => p.Hex).ToList();
            SettingsService.Save();
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

        // ---------------- Picking (§3): screen, image, recent history ----------------

        public ObservableCollection<RecentPick> RecentPicks { get; } = new();

        private const int MaxRecentPicks = 20;

        /// <summary>§3 "System-wide eyedropper" — click anywhere on screen to capture the color.</summary>
        [RelayCommand]
        private async Task PickFromScreenAsync()
        {
            if (ScreenPickerOpen) return;
            if (!ScreenPickerService.IsSupported)
            {
                StatusMessage = "Screen picking uses native Windows interop — not available on this OS yet.";
                return;
            }

            if (GetOwnerWindow() is not { } owner) return;

            ScreenPickerOpen = true;
            try
            {
                var picker = new Views.ScreenPickerWindow();
                var color = await picker.ShowDialog<Color?>(owner);
                if (color is null)
                {
                    StatusMessage = "Screen pick cancelled.";
                    return;
                }

                ApplyPickedColor(color.Value, "screen");
            }
            finally
            {
                ScreenPickerOpen = false;
            }
        }

        /// <summary>§3 "In-app eyedropper (pick from loaded image)".</summary>
        [RelayCommand]
        private async Task PickFromImageAsync()
        {
            var bitmap = await FilePickerHelper.PickImageAsync();
            if (bitmap is null)
            {
                StatusMessage = "No image selected.";
                return;
            }

            if (GetOwnerWindow() is not { } owner)
            {
                bitmap.Dispose();
                return;
            }

            var picker = new Views.ImageEyedropperWindow(bitmap);
            var color = await picker.ShowDialog<Color?>(owner);
            if (color is null)
            {
                StatusMessage = "Eyedropper cancelled.";
                return;
            }

            ApplyPickedColor(color.Value, "image");
        }

        /// <summary>Applies a picked color to the color panel and records it in the history.</summary>
        private void ApplyPickedColor(Color color, string source)
        {
            ApplyToBaseColor(color);

            // Newest first, no duplicates, capped at 20 (§3 history).
            var existing = RecentPicks.FirstOrDefault(p => p.Color == color);
            if (existing is not null) RecentPicks.Remove(existing);
            RecentPicks.Insert(0, new RecentPick(color));
            while (RecentPicks.Count > MaxRecentPicks)
                RecentPicks.RemoveAt(RecentPicks.Count - 1);
            PersistRecentPicks();

            StatusMessage = $"Picked {ColorMathService.ToHex(color)} from {source} — add it to the palette when happy.";
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

        [RelayCommand]
        private void ApplyRecentPick(RecentPick? pick)
        {
            if (pick is null) return;
            ApplyToBaseColor(pick.Color);
            StatusMessage = $"Loaded {pick.Hex} ({pick.Name}) into the color panel.";
        }

        /// <summary>§3 "Global hotkey to trigger picker" — Ctrl+Alt+P by default (rebindable in Settings).</summary>
        private void OnGlobalHotkeyRaised()
        {
            if (PickFromScreenCommand.CanExecute(null))
                PickFromScreenCommand.Execute(null);
        }

        private static Avalonia.Controls.Window? GetOwnerWindow()
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
                RaiseMockupPropertiesChanged();
            }
        }

        partial void OnHueChanged(double value)
        {
            RaiseColorPropertiesChanged();
            OnPropertyChanged(nameof(SaturationTrackBrush));
        }

        partial void OnSaturationChanged(double value) => RaiseColorPropertiesChanged();
        partial void OnLightnessChanged(double value) => RaiseColorPropertiesChanged();
        partial void OnTemperatureChanged(double value) => RaiseColorPropertiesChanged();

        partial void OnAlphaChanged(double value)
        {
            OnPropertyChanged(nameof(AlphaLabel));
            RaiseColorPropertiesChanged();
        }

        partial void OnSelectedColorBlindModeChanged(ColorBlindMode value)
        {
            foreach (var swatch in Palette)
            {
                swatch.ColorBlindMode = value;
            }
        }

        // ---------------- Inspector: color science (§4) ----------------

        [ObservableProperty]
        private Color _contrastBackground = Colors.White;

        // Live conversion readouts for the base color.
        public string ScienceHex =>
            BaseColor.A == 255
                ? ColorMathService.ToHex(BaseColor)
                : $"{ColorMathService.ToHex(BaseColor)}{BaseColor.A:X2}";

        public string ScienceRgb =>
            BaseColor.A == 255
                ? $"{BaseColor.R}, {BaseColor.G}, {BaseColor.B}"
                : $"{BaseColor.R}, {BaseColor.G}, {BaseColor.B}, {Math.Round(BaseColor.A / 255.0, 2)}";

        public string ScienceHsl
        {
            get
            {
                var (h, s, l) = ColorMathService.RgbToHsl(BaseColor);
                return $"{h:N0}°, {s * 100:N0}%, {l * 100:N0}%";
            }
        }

        public string ScienceHsv
        {
            get
            {
                var hsv = ColorMathService.RgbToHsv(BaseColor);
                return $"{hsv.H:N0}°, {hsv.S * 100:N0}%, {hsv.V * 100:N0}%";
            }
        }

        public string ScienceLab
        {
            get
            {
                var lab = ColorMathService.RgbToLab(BaseColor);
                var lch = ColorMathService.LabToLch(lab);
                return $"{lab.L:N1}, {lab.A:N1}, {lab.B:N1}\nLCH {lch.L:N1}, {lch.C:N1}, {lch.H:N0}°";
            }
        }

        public string ScienceOklch
        {
            get
            {
                var ok = ColorMathService.RgbToOklch(BaseColor);
                return $"{ok.L:N3}, {ok.C:N3}, {ok.H:N0}°";
            }
        }

        // Contrast checker (WCAG + APCA) of the base color as text over ContrastBackground.
        public IBrush ContrastBackgroundBrush => new SolidColorBrush(ContrastBackground);

        public string ContrastPairLabel =>
            $"{ColorMathService.ToHex(BaseColor)} text on {ColorMathService.ToHex(ContrastBackground)}";

        public double ContrastRatioNow => ContrastService.ContrastRatio(BaseColor, ContrastBackground);

        public string ContrastRatioLabel => $"{ContrastRatioNow:N2}:1";
        public string ContrastRatingLabel => ContrastService.Rate(ContrastRatioNow);
        public string ContrastRatingLargeLabel => ContrastService.Rate(ContrastRatioNow, largeText: true);

        public double ApcaNow => ContrastService.Apca(BaseColor, ContrastBackground);
        public string ApcaLabel => $"Lc {ApcaNow:N0}";
        public string ApcaRatingLabel => ContrastService.RateApca(ApcaNow);

        partial void OnContrastBackgroundChanged(Color value) => RaiseContrastPropertiesChanged();

        private void RaiseContrastPropertiesChanged()
        {
            OnPropertyChanged(nameof(ContrastBackgroundBrush));
            OnPropertyChanged(nameof(ContrastPairLabel));
            OnPropertyChanged(nameof(ContrastRatioNow));
            OnPropertyChanged(nameof(ContrastRatioLabel));
            OnPropertyChanged(nameof(ContrastRatingLabel));
            OnPropertyChanged(nameof(ContrastRatingLargeLabel));
            OnPropertyChanged(nameof(ApcaNow));
            OnPropertyChanged(nameof(ApcaLabel));
            OnPropertyChanged(nameof(ApcaRatingLabel));
        }

        [RelayCommand]
        private void UseWhiteContrastBackground() => ContrastBackground = Colors.White;

        [RelayCommand]
        private void UseBlackContrastBackground() => ContrastBackground = Colors.Black;

        [RelayCommand]
        private async Task PickContrastBackgroundAsync()
        {
            var picked = await PickColorAsync(ContrastBackground);
            if (picked is null) return;
            ContrastBackground = picked.Value;
            StatusMessage = "Contrast background updated.";
        }

        // Tints / shades / tones generators (§4 "Generate tints / shades").
        [RelayCommand] private void AddTints() => AppendGenerated(ColorMathService.Tints(BaseColor), "tint");
        [RelayCommand] private void AddShades() => AppendGenerated(ColorMathService.Shades(BaseColor), "shade");
        [RelayCommand] private void AddTones() => AppendGenerated(ColorMathService.Tones(BaseColor), "tone");

        private void AppendGenerated(List<Color> colors, string kind)
        {
            PushHistory();
            foreach (var c in colors)
                Palette.Add(CreateSwatch(c, ColorNamingService.GetClosestName(c)));
            StatusMessage = $"Added {colors.Count} {kind}s derived from {ColorMathService.ToHex(BaseColor)}.";
        }

        private void RaiseColorPropertiesChanged()
        {
            OnPropertyChanged(nameof(BaseColor));
            OnPropertyChanged(nameof(BaseColorBrush));
            OnPropertyChanged(nameof(BaseColorName));
            OnPropertyChanged(nameof(BaseForegroundBrush));
            OnPropertyChanged(nameof(BaseHex));
            OnPropertyChanged(nameof(AlphaTrackBrush));
            OnPropertyChanged(nameof(ScienceHex));
            OnPropertyChanged(nameof(ScienceRgb));
            OnPropertyChanged(nameof(ScienceHsl));
            OnPropertyChanged(nameof(ScienceHsv));
            OnPropertyChanged(nameof(ScienceLab));
            OnPropertyChanged(nameof(ScienceOklch));
            RaiseContrastPropertiesChanged();
        }

        private static Color ApplyTemperature(Color c, double temperature)
        {
            // A simple photo-style temperature shift: push red up / blue down
            // for "warm", or the reverse for "cool". Purely a visual tint on
            // top of the HSL-derived color, not a physical color-temperature model.
            var t = Math.Clamp(temperature, -100, 100) / 100.0;
            var r = c.R + t * 40;
            var b = c.B - t * 40;
            return Color.FromRgb(ToByte(r), c.G, ToByte(b));
        }

        private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);

        private static bool TryParseHex(string? input, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var text = input.Trim().TrimStart('#');
            if (text.Length != 6) return false;

            if (byte.TryParse(text.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                byte.TryParse(text.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                byte.TryParse(text.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                color = Color.FromRgb(r, g, b);
                return true;
            }

            return false;
        }

        private static LinearGradientBrush CreateHorizontalGradient(params (Color Color, double Offset)[] stops)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative)
            };

            foreach (var (color, offset) in stops)
            {
                brush.GradientStops.Add(new GradientStop(color, offset));
            }

            return brush;
        }

        // Wires each swatch's card buttons back to this ViewModel's commands
        // so the UI never needs a reference back up to the parent DataContext.
        private ColorSwatch CreateSwatch(Color color, string name)
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

        private void PushHistory()
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
        private void AddCurrentColor()
        {
            PushHistory();

            var name = ColorNamingService.GetClosestName(BaseColor);
            Palette.Add(CreateSwatch(BaseColor, name));
            StatusMessage = $"Added \"{name}\" to the palette.";
        }

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

        // Coolors-style generator: press the "Press Space to Shuffle" button
        // (or hit Space anywhere — see MainWindow.axaml.cs) and every unlocked
        // swatch is rerolled, in place, keeping the palette's order and size
        // intact. The board is padded up to 5 slots first if it's smaller.
        // The "Harmonize" tool controls *how* colors are rerolled: left on
        // Random, each unlocked swatch gets an independent random color;
        // pick a specific rule and the whole set is rerolled together from
        // that rule, cycling its colors across however many slots there are.
        [RelayCommand]
        private void GeneratePalette()
        {
            PushHistory();

            var rng = Random.Shared;

            while (Palette.Count < DefaultPaletteSize)
            {
                var seedColor = ColorHarmonyService.RandomPleasant(rng);
                Palette.Add(CreateSwatch(seedColor, ColorNamingService.GetClosestName(seedColor)));
            }

            List<Color>? harmonyColors = SelectedHarmony == HarmonyType.Random
                ? null
                : ColorHarmonyService.Generate(BaseColor, SelectedHarmony);

            var colorIndex = 0;
            var replaced = 0;

            foreach (var swatch in Palette)
            {
                if (swatch.IsLocked) continue;

                var next = harmonyColors is { Count: > 0 }
                    ? harmonyColors[colorIndex++ % harmonyColors.Count]
                    : ColorHarmonyService.RandomPleasant(rng);

                swatch.Color = next;
                swatch.Name = ColorNamingService.GetClosestName(next);
                replaced++;
            }

            StatusMessage = replaced == 0
                ? "Every color is locked — unlock one to regenerate it."
                : harmonyColors is null
                    ? $"Generated {replaced} new color{(replaced == 1 ? "" : "s")}."
                    : $"Generated {replaced} new color{(replaced == 1 ? "" : "s")} ({SelectedHarmony}).";
        }

        [RelayCommand]
        private async Task ExtractFromImageAsync()
        {
            var bitmap = await FilePickerHelper.PickImageAsync();
            if (bitmap is null)
            {
                StatusMessage = "No image selected.";
                return;
            }

            PushHistory();

            List<Color> colors;
            try
            {
                colors = ImageColorExtractionService.ExtractDominantColors(bitmap, DefaultPaletteSize);
            }
            finally
            {
                bitmap.Dispose();
            }

            if (colors.Count == 0)
            {
                StatusMessage = "Couldn't find usable colors in that image.";
                return;
            }

            while (Palette.Count < colors.Count)
            {
                Palette.Add(CreateSwatch(Colors.Gray, "Color"));
            }

            var index = 0;
            var replaced = 0;

            foreach (var swatch in Palette)
            {
                if (swatch.IsLocked || index >= colors.Count) continue;

                var next = colors[index++];
                swatch.Color = next;
                swatch.Name = ColorNamingService.GetClosestName(next);
                replaced++;
            }

            StatusMessage = $"Extracted {replaced} color{(replaced == 1 ? "" : "s")} from the image.";
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

        /// <summary>§2 "Paste color from clipboard" — reads any supported notation into the color panel.</summary>
        [RelayCommand]
        private async Task PasteColorAsync()
        {
            var text = await ClipboardHelper.GetTextAsync();
            if (!ColorFormatService.TryParse(text, out var color))
            {
                StatusMessage = string.IsNullOrWhiteSpace(text)
                    ? "Clipboard is empty."
                    : "Couldn't read a color from the clipboard.";
                return;
            }

            ApplyToBaseColor(color);
            StatusMessage = $"Pasted {ColorMathService.ToHex(color)} from clipboard.";
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

        /// <summary>§2 "Edit swatch color (picker)" — opens the spectrum dialog for the base color.</summary>
        [RelayCommand]
        private async Task OpenColorPickerAsync()
        {
            var picked = await PickColorAsync(BaseColor);
            if (picked is null)
            {
                StatusMessage = "Color pick cancelled.";
                return;
            }

            ApplyToBaseColor(picked.Value);
            StatusMessage = $"Base color set to {ColorNamingService.GetClosestName(picked.Value)}.";
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

        private void ApplyToBaseColor(Color color)
        {
            var (h, s, l) = ColorHarmonyService.ToHsl(color);
            Hue = h;
            Saturation = s * 100.0;
            Lightness = l * 100.0;
            Temperature = 0;
            Alpha = color.A / 255.0 * 100.0;
        }

        private static async Task<Color?> PickColorAsync(Color initial)
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

        [RelayCommand]
        private void BuildExportPreview()
        {
            ExportPreview = PaletteExportService.Export(Palette, SelectedExportFormat);
            StatusMessage = $"Export preview built ({SelectedExportFormat}).";
        }

        [RelayCommand]
        private async Task CopyExportAsync()
        {
            if (string.IsNullOrEmpty(ExportPreview)) BuildExportPreview();
            await ClipboardHelper.SetTextAsync(ExportPreview);
            StatusMessage = "Copied export text to clipboard.";
        }

        /// <summary>§6 "Export presets": writes the selected format straight to a local file
        /// (text formats directly, ASE/ACO as their binary encodings).</summary>
        [RelayCommand]
        private async Task ExportToFileAsync()
        {
            var format = SelectedExportFormat;
            var extension = PaletteExportService.ExtensionFor(format);
            var path = await PaletteFileService.PickSaveFileAsync(
                $"palette-{DateTime.Now:yyyyMMdd-HHmmss}", extension, $"{format} (*.{extension})");
            if (string.IsNullOrEmpty(path))
            {
                StatusMessage = "Export cancelled.";
                return;
            }

            try
            {
                switch (format)
                {
                    case ExportFormat.Ase:
                        await System.IO.File.WriteAllBytesAsync(path, PaletteBinaryExportService.WriteAse(Palette));
                        break;
                    case ExportFormat.Aco:
                        await System.IO.File.WriteAllBytesAsync(path, PaletteBinaryExportService.WriteAco(Palette));
                        break;
                    default:
                        await System.IO.File.WriteAllTextAsync(path, PaletteExportService.Export(Palette, format));
                        break;
                }

                StatusMessage = $"Exported {Palette.Count} color{(Palette.Count == 1 ? "" : "s")} to {System.IO.Path.GetFileName(path)}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Couldn't export: {ex.Message}";
            }
        }

        /// <summary>§6 "Export as PNG (swatch sheet)".</summary>
        [RelayCommand]
        private async Task ExportPngAsync()
        {
            var path = await PaletteFileService.PickSaveFileAsync(
                $"palette-{DateTime.Now:yyyyMMdd-HHmmss}", "png", "PNG swatch sheet (*.png)");
            if (string.IsNullOrEmpty(path))
            {
                StatusMessage = "Export cancelled.";
                return;
            }

            try
            {
                PngExportService.Export(Palette, path);
                StatusMessage = $"PNG swatch sheet saved to {System.IO.Path.GetFileName(path)}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Couldn't render PNG: {ex.Message}";
            }
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
