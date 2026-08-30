using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spectrum.Models;
using Spectrum.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Spectrum.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public ObservableCollection<ColorSwatch> Palette { get; } = new();

        public Array HarmonyTypes { get; } = Enum.GetValues(typeof(HarmonyType));
        public Array ExportFormats { get; } = Enum.GetValues(typeof(ExportFormat));

        // Snapshots of the palette for Undo/Redo. Each entry is the full
        // palette state immediately before a mutating action.
        private readonly Stack<List<PaletteSwatchDto>> _undoStack = new();
        private readonly Stack<List<PaletteSwatchDto>> _redoStack = new();

        [ObservableProperty]
        private double _baseR = 76;

        [ObservableProperty]
        private double _baseG = 139;

        [ObservableProperty]
        private double _baseB = 245;

        [ObservableProperty]
        private double _baseA = 255;

        [ObservableProperty]
        private string _newSwatchName = "Color";

        [ObservableProperty]
        private HarmonyType _selectedHarmony = HarmonyType.Complementary;

        [ObservableProperty]
        private ExportFormat _selectedExportFormat = ExportFormat.Json;

        [ObservableProperty]
        private string _exportPreview = string.Empty;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private int _paletteCount;

        // True when the hex quick-entry box currently contains unparsable text.
        [ObservableProperty]
        private bool _isHexInvalid;

        public string PaletteCountLabel => PaletteCount == 1 ? "1 color" : $"{PaletteCount} colors";

        partial void OnPaletteCountChanged(int value) => OnPropertyChanged(nameof(PaletteCountLabel));

        public Color BaseColor => Color.FromArgb(ToByte(BaseA), ToByte(BaseR), ToByte(BaseG), ToByte(BaseB));

        public IBrush BaseColorBrush => new SolidColorBrush(BaseColor);

        public string BaseHex
        {
            get => $"#{BaseColor.R:X2}{BaseColor.G:X2}{BaseColor.B:X2}";
            set
            {
                if (TryParseHex(value, out var color))
                {
                    IsHexInvalid = false;
                    BaseR = color.R;
                    BaseG = color.G;
                    BaseB = color.B;
                }
                else
                {
                    IsHexInvalid = true;
                    StatusMessage = "Invalid hex color \u2014 use the format #RRGGBB.";
                }
            }
        }

        public MainWindowViewModel()
        {
            // Keep the header's "N colors" pill in sync, and re-evaluate which
            // move/clear buttons should be enabled, no matter what caused the
            // palette to change (add, remove, clear, harmony, reorder...).
            Palette.CollectionChanged += (_, _) =>
            {
                PaletteCount = Palette.Count;
                MoveSwatchUpCommand.NotifyCanExecuteChanged();
                MoveSwatchDownCommand.NotifyCanExecuteChanged();
                ClearPaletteCommand.NotifyCanExecuteChanged();
            };
        }

        partial void OnBaseRChanged(double value) => RaiseColorPropertiesChanged();
        partial void OnBaseGChanged(double value) => RaiseColorPropertiesChanged();
        partial void OnBaseBChanged(double value) => RaiseColorPropertiesChanged();
        partial void OnBaseAChanged(double value) => RaiseColorPropertiesChanged();

        private void RaiseColorPropertiesChanged()
        {
            OnPropertyChanged(nameof(BaseColor));
            OnPropertyChanged(nameof(BaseColorBrush));
            OnPropertyChanged(nameof(BaseHex));
        }

        private static byte ToByte(double value) => (byte)Math.Clamp(value, 0, 255);

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

        // Wires each swatch's card buttons back to this ViewModel's commands
        // so the UI never needs a reference back up to the parent DataContext.
        private ColorSwatch CreateSwatch(Color color, string name)
        {
            return new ColorSwatch(color, name)
            {
                CopyCommand = CopyHexCommand,
                RemoveCommand = RemoveSwatchCommand,
                MoveUpCommand = MoveSwatchUpCommand,
                MoveDownCommand = MoveSwatchDownCommand
            };
        }

        // ===================== Undo / Redo =====================

        private List<PaletteSwatchDto> Snapshot() =>
            Palette.Select(s => new PaletteSwatchDto(s.Name, s.Color.A, s.Color.R, s.Color.G, s.Color.B, s.IsLocked)).ToList();

        // Call before any operation that adds, removes, replaces or reorders
        // swatches, so that operation becomes undoable.
        private void PushUndoSnapshot()
        {
            _undoStack.Push(Snapshot());
            _redoStack.Clear();
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }

        private void RestoreSnapshot(List<PaletteSwatchDto> snapshot)
        {
            Palette.Clear();
            foreach (var item in snapshot)
            {
                var swatch = CreateSwatch(Color.FromArgb(item.A, item.R, item.G, item.B), item.Name);
                swatch.IsLocked = item.IsLocked;
                Palette.Add(swatch);
            }
        }

        [RelayCommand(CanExecute = nameof(CanUndo))]
        private void Undo()
        {
            if (_undoStack.Count == 0) return;

            _redoStack.Push(Snapshot());
            RestoreSnapshot(_undoStack.Pop());
            StatusMessage = "Undid last change.";
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }

        private bool CanUndo() => _undoStack.Count > 0;

        [RelayCommand(CanExecute = nameof(CanRedo))]
        private void Redo()
        {
            if (_redoStack.Count == 0) return;

            _undoStack.Push(Snapshot());
            RestoreSnapshot(_redoStack.Pop());
            StatusMessage = "Redid change.";
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }

        private bool CanRedo() => _redoStack.Count > 0;

        // ===================== Palette editing =====================

        [RelayCommand]
        private void AddCurrentColor()
        {
            PushUndoSnapshot();
            var name = string.IsNullOrWhiteSpace(NewSwatchName) ? "Color" : NewSwatchName;
            Palette.Add(CreateSwatch(BaseColor, name));
            StatusMessage = $"Added \"{name}\".";
        }

        [RelayCommand]
        private void RemoveSwatch(ColorSwatch? swatch)
        {
            if (swatch is null) return;
            PushUndoSnapshot();
            Palette.Remove(swatch);
            StatusMessage = "Swatch removed.";
        }

        [RelayCommand(CanExecute = nameof(CanClearPalette))]
        private void ClearPalette()
        {
            PushUndoSnapshot();
            Palette.Clear();
            StatusMessage = "Palette cleared.";
        }

        private bool CanClearPalette() => Palette.Count > 0;

        [RelayCommand(CanExecute = nameof(CanMoveSwatchUp))]
        private void MoveSwatchUp(ColorSwatch? swatch)
        {
            if (swatch is null) return;
            var index = Palette.IndexOf(swatch);
            if (index <= 0) return;

            PushUndoSnapshot();
            Palette.Move(index, index - 1);
        }

        private bool CanMoveSwatchUp(ColorSwatch? swatch)
        {
            if (swatch is null) return false;
            return Palette.IndexOf(swatch) > 0;
        }

        [RelayCommand(CanExecute = nameof(CanMoveSwatchDown))]
        private void MoveSwatchDown(ColorSwatch? swatch)
        {
            if (swatch is null) return;
            var index = Palette.IndexOf(swatch);
            if (index < 0 || index >= Palette.Count - 1) return;

            PushUndoSnapshot();
            Palette.Move(index, index + 1);
        }

        private bool CanMoveSwatchDown(ColorSwatch? swatch)
        {
            if (swatch is null) return false;
            var index = Palette.IndexOf(swatch);
            return index >= 0 && index < Palette.Count - 1;
        }

        // Called from MainWindow's drag-and-drop handling to move a swatch
        // to wherever it was dropped.
        public void ReorderSwatch(ColorSwatch source, ColorSwatch target)
        {
            var oldIndex = Palette.IndexOf(source);
            var newIndex = Palette.IndexOf(target);
            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex) return;

            PushUndoSnapshot();
            Palette.Move(oldIndex, newIndex);
            StatusMessage = $"Moved \"{source.Name}\" to position {newIndex + 1}.";
        }

        [RelayCommand]
        private void GenerateHarmony()
        {
            PushUndoSnapshot();
            var colors = ColorHarmonyService.Generate(BaseColor, SelectedHarmony);
            foreach (var c in colors)
            {
                Palette.Add(CreateSwatch(c, SelectedHarmony.ToString()));
            }

            StatusMessage = $"Added {colors.Count} colors from {SelectedHarmony} harmony.";
        }

        // "Shuffle": a fresh random base color + harmony, replacing the palette
        // entirely except for any swatches the user has locked in place.
        // Alpha is reset to fully opaque so shuffles don't produce surprise
        // transparency.
        [RelayCommand]
        private void RandomizePalette()
        {
            PushUndoSnapshot();

            var rng = Random.Shared;
            BaseR = rng.Next(0, 256);
            BaseG = rng.Next(0, 256);
            BaseB = rng.Next(0, 256);
            BaseA = 255;

            var harmonies = (HarmonyType[])Enum.GetValues(typeof(HarmonyType));
            SelectedHarmony = harmonies[rng.Next(harmonies.Length)];

            var locked = Palette.Where(s => s.IsLocked).ToList();
            Palette.Clear();
            foreach (var swatch in locked)
            {
                Palette.Add(swatch);
            }

            var colors = ColorHarmonyService.Generate(BaseColor, SelectedHarmony);
            foreach (var c in colors)
            {
                Palette.Add(CreateSwatch(c, SelectedHarmony.ToString()));
            }

            StatusMessage = locked.Count > 0
                ? $"Shuffled ({SelectedHarmony}) \u2014 kept {locked.Count} locked color(s)."
                : $"Shuffled ({SelectedHarmony}).";
        }

        [RelayCommand]
        private async Task CopyHexAsync(ColorSwatch? swatch)
        {
            if (swatch is null) return;
            await ClipboardHelper.SetTextAsync(swatch.Hex);
            StatusMessage = $"Copied {swatch.Hex} to clipboard.";
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

        // ===================== Save / Load palette =====================

        [RelayCommand]
        private async Task SavePaletteAsync()
        {
            var path = await PaletteFileService.PickSaveFileAsync("palette.json");
            if (path is null) return;

            try
            {
                var dto = Palette
                    .Select(s => new PaletteSwatchDto(s.Name, s.Color.A, s.Color.R, s.Color.G, s.Color.B, s.IsLocked))
                    .ToList();

                var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(path, json);
                StatusMessage = $"Saved palette to {Path.GetFileName(path)}.";
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
            if (path is null) return;

            try
            {
                var json = await File.ReadAllTextAsync(path);
                var dto = JsonSerializer.Deserialize<List<PaletteSwatchDto>>(json);
                if (dto is null)
                {
                    StatusMessage = "That file didn't contain a recognizable palette.";
                    return;
                }

                PushUndoSnapshot();
                RestoreSnapshot(dto);
                StatusMessage = $"Loaded palette from {Path.GetFileName(path)}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Couldn't load palette: {ex.Message}";
            }
        }
    }
}
