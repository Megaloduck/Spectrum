using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spectrum.Models;
using Spectrum.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Spectrum.ViewModels
{
    /// <summary>
    /// Ctrl+2 · Preview — the live mockup derived from the palette, the light /
    /// dark variant generators and side-by-side comparison. The board layout
    /// (Row / Grid / List / Compact) is shared board state and stays on
    /// <see cref="MainWindowViewModel"/>.
    /// </summary>
    public partial class PreviewWorkspaceViewModel : ViewModelBase
    {
        private readonly MainWindowViewModel _shell;

        public PreviewWorkspaceViewModel(MainWindowViewModel shell) => _shell = shell;

        // Live UI mockup brushes, derived from the current palette.
        private Color PaletteAt(int index, Color fallback) =>
            _shell.Palette.Count == 0
                ? fallback
                : _shell.Palette[Math.Clamp(index, 0, _shell.Palette.Count - 1)].Color;

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
            if (_shell.Palette.Count == 0) return Color.FromRgb(0x1E, 0x20, 0x25);

            return _shell.Palette
                .Select(s => s.Color)
                .OrderByDescending(c => ContrastService.ContrastRatio(c, MockupBackground))
                .First();
        }

        private static Color ReadableOn(Color color) =>
            (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0 > 0.6
                ? Color.FromRgb(0x1E, 0x20, 0x25)
                : Colors.White;

        /// <summary>Called by the shell whenever the palette changes so the mockup follows.</summary>
        public void RaiseMockupPropertiesChanged()
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
            if (_shell.Palette.Count == 0)
            {
                _shell.StatusMessage = "Add some colors before generating a variant.";
                return;
            }

            var baseName = PaletteLibraryService.FindPalette(_shell.ActivePaletteId)?.Name ?? "Palette";
            var suffix = light ? "Light" : "Dark";
            var swatches = _shell.Palette.Select(s =>
            {
                var derived = light
                    ? ColorMathService.Mix(s.Color, Colors.White, 0.30)
                    : ColorMathService.Mix(s.Color, Colors.Black, 0.35);
                return new PaletteSwatchDto(
                    light ? $"{s.Name} tint" : $"{s.Name} shade",
                    derived.A, derived.R, derived.G, derived.B, s.IsLocked);
            }).ToList();

            var dto = PaletteLibraryService.AddPalette($"{baseName} ({suffix})", swatches);
            _shell.Library.AddAndSelect(dto);
            _shell.StatusMessage = $"Generated {suffix.ToLowerInvariant()} mode variant with {swatches.Count} colors.";
        }

        /// <summary>§8 "Side-by-side palette comparison".</summary>
        [RelayCommand]
        private async Task ComparePalettesAsync()
        {
            if (MainWindowViewModel.GetOwnerWindow() is not { } owner) return;

            var current = PaletteLibraryService.FindPalette(_shell.ActivePaletteId)
                          ?? PaletteLibraryService.Library.Palettes.FirstOrDefault();

            var window = new Views.CompareWindow(current);
            await window.ShowDialog(owner);
        }
    }
}
