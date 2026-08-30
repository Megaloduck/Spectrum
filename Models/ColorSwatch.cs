using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Spectrum.Services;
using System.Windows.Input;

namespace Spectrum.Models
{
    /// <summary>
    /// A single color in the palette. Commands are wired up by the
    /// owning ViewModel (see MainWindowViewModel.CreateSwatch) so the
    /// card in the UI can call back into copy/remove/reorder logic
    /// without needing a reference to the parent DataContext.
    /// </summary>
    public partial class ColorSwatch : ObservableObject
    {
        [ObservableProperty]
        private Color _color;

        [ObservableProperty]
        private string _name = "Color";

        // Locked swatches survive "Shuffle" instead of being replaced.
        [ObservableProperty]
        private bool _isLocked;

        public string Hex => $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";

        // Only shown in the UI when the swatch isn't fully opaque.
        public string HexRgba => $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}{Color.A:X2}";

        public bool IsTranslucent => Color.A < 255;

        public IBrush Brush => new SolidColorBrush(Color);

        // Picks readable black or white text/icons for whatever the swatch color is,
        // using the standard relative-luminance formula.
        public IBrush ForegroundBrush =>
            (0.2126 * Color.R + 0.7152 * Color.G + 0.0722 * Color.B) / 255.0 > 0.6
                ? new SolidColorBrush(Color.FromRgb(0x1E, 0x20, 0x25))
                : Brushes.White;

        // WCAG contrast ratios against pure white/black, shown in the rename
        // flyout so it doubles as a quick accessibility check for the swatch.
        public double ContrastWithWhite => ContrastService.ContrastRatio(Color, Colors.White);
        public double ContrastWithBlack => ContrastService.ContrastRatio(Color, Colors.Black);

        public string ContrastSummary =>
            $"vs white {ContrastWithWhite:F1} ({ContrastService.Rate(ContrastWithWhite)}) \u00b7 " +
            $"vs black {ContrastWithBlack:F1} ({ContrastService.Rate(ContrastWithBlack)})";

        public ICommand? CopyCommand { get; set; }
        public ICommand? RemoveCommand { get; set; }
        public ICommand? MoveUpCommand { get; set; }
        public ICommand? MoveDownCommand { get; set; }

        partial void OnColorChanged(Color value)
        {
            OnPropertyChanged(nameof(Hex));
            OnPropertyChanged(nameof(HexRgba));
            OnPropertyChanged(nameof(IsTranslucent));
            OnPropertyChanged(nameof(Brush));
            OnPropertyChanged(nameof(ForegroundBrush));
            OnPropertyChanged(nameof(ContrastWithWhite));
            OnPropertyChanged(nameof(ContrastWithBlack));
            OnPropertyChanged(nameof(ContrastSummary));
        }

        public ColorSwatch()
        {
        }

        public ColorSwatch(Color color, string name = "Color")
        {
            _color = color;
            _name = name;
        }
    }
}
