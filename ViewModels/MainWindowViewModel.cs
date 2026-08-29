using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spectrum.Models;
using Spectrum.Services;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;

namespace Spectrum.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public ObservableCollection<ColorSwatch> Palette { get; } = new();

        public Array HarmonyTypes { get; } = Enum.GetValues(typeof(HarmonyType));
        public Array ExportFormats { get; } = Enum.GetValues(typeof(ExportFormat));

        [ObservableProperty]
        private double _baseR = 76;

        [ObservableProperty]
        private double _baseG = 139;

        [ObservableProperty]
        private double _baseB = 245;

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

        public Color BaseColor => Color.FromRgb(ToByte(BaseR), ToByte(BaseG), ToByte(BaseB));

        public IBrush BaseColorBrush => new SolidColorBrush(BaseColor);

        public string BaseHex
        {
            get => $"#{BaseColor.R:X2}{BaseColor.G:X2}{BaseColor.B:X2}";
            set
            {
                if (TryParseHex(value, out var color))
                {
                    BaseR = color.R;
                    BaseG = color.G;
                    BaseB = color.B;
                }
            }
        }

        partial void OnBaseRChanged(double value) => RaiseColorPropertiesChanged();
        partial void OnBaseGChanged(double value) => RaiseColorPropertiesChanged();
        partial void OnBaseBChanged(double value) => RaiseColorPropertiesChanged();

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

        [RelayCommand]
        private void AddCurrentColor()
        {
            var name = string.IsNullOrWhiteSpace(NewSwatchName) ? "Color" : NewSwatchName;
            Palette.Add(CreateSwatch(BaseColor, name));
            StatusMessage = $"Added \"{name}\".";
        }

        [RelayCommand]
        private void RemoveSwatch(ColorSwatch? swatch)
        {
            if (swatch is null) return;
            Palette.Remove(swatch);
            StatusMessage = "Swatch removed.";
        }

        [RelayCommand]
        private void ClearPalette()
        {
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
        private void GenerateHarmony()
        {
            var colors = ColorHarmonyService.Generate(BaseColor, SelectedHarmony);
            foreach (var c in colors)
            {
                Palette.Add(CreateSwatch(c, SelectedHarmony.ToString()));
            }

            StatusMessage = $"Added {colors.Count} colors from {SelectedHarmony} harmony.";
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
