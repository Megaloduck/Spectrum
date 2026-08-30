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
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Spectrum.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private const int DefaultPaletteSize = 5;

        public ObservableCollection<ColorSwatch> Palette { get; } = new();

        public Array HarmonyTypes { get; } = Enum.GetValues(typeof(HarmonyType));
        public Array ExportFormats { get; } = Enum.GetValues(typeof(ExportFormat));
        public Array ColorBlindModes { get; } = Enum.GetValues(typeof(ColorBlindMode));

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
                return ApplyTemperature(hslColor, Temperature);
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

        // ---- Undo/redo history (covers add/remove/clear/generate/extract —
        // ---- see PushHistory call sites. In-place edits like renaming or
        // ---- toggling a lock aren't tracked.)
        private readonly Stack<List<SwatchSnapshot>> _undoStack = new();
        private readonly Stack<List<SwatchSnapshot>> _redoStack = new();
        private bool _isRestoringHistory;

        private readonly record struct SwatchSnapshot(Color Color, string Name, bool IsLocked);

        public MainWindowViewModel()
        {
            // Keep the header's "N colors" pill in sync no matter what
            // caused the palette to change (add, remove, clear, generate...).
            Palette.CollectionChanged += (_, _) => PaletteCount = Palette.Count;

            SeedInitialPalette();
        }

        partial void OnHueChanged(double value)
        {
            RaiseColorPropertiesChanged();
            OnPropertyChanged(nameof(SaturationTrackBrush));
        }

        partial void OnSaturationChanged(double value) => RaiseColorPropertiesChanged();
        partial void OnLightnessChanged(double value) => RaiseColorPropertiesChanged();
        partial void OnTemperatureChanged(double value) => RaiseColorPropertiesChanged();

        partial void OnSelectedColorBlindModeChanged(ColorBlindMode value)
        {
            foreach (var swatch in Palette)
            {
                swatch.ColorBlindMode = value;
            }
        }

        private void RaiseColorPropertiesChanged()
        {
            OnPropertyChanged(nameof(BaseColor));
            OnPropertyChanged(nameof(BaseColorBrush));
            OnPropertyChanged(nameof(BaseColorName));
            OnPropertyChanged(nameof(BaseForegroundBrush));
            OnPropertyChanged(nameof(BaseHex));
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
            return new ColorSwatch(color, name)
            {
                ColorBlindMode = SelectedColorBlindMode,
                CopyCommand = CopyHexCommand,
                RemoveCommand = RemoveSwatchCommand,
                MoveUpCommand = MoveSwatchUpCommand,
                MoveDownCommand = MoveSwatchDownCommand
            };
        }

        private void SeedInitialPalette()
        {
            var rng = Random.Shared;
            for (var i = 0; i < DefaultPaletteSize; i++)
            {
                var c = ColorHarmonyService.RandomPleasant(rng);
                Palette.Add(CreateSwatch(c, ColorNamingService.GetClosestName(c)));
            }
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
            Palette.Clear();
            foreach (var s in snapshot)
            {
                var swatch = CreateSwatch(s.Color, s.Name);
                swatch.IsLocked = s.IsLocked;
                Palette.Add(swatch);
            }
            _isRestoringHistory = false;
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
    }
}