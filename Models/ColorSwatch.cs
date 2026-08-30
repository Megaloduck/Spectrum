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

        // Locked swatches survive "Shuffle"/Generate instead of being replaced.
        [ObservableProperty]
        private bool _isLocked;

        // Set by the ViewModel whenever the toolbar's "Color Blind" tool
        // changes. This only affects how the swatch is *drawn* (Brush /
        // ForegroundBrush below) — Hex, Name, and everything you copy or
        // export always reflect the real, unsimulated color.
        [ObservableProperty]
        private ColorBlindMode _colorBlindMode = ColorBlindMode.None;

        public string Hex => $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";

        public Color DisplayColor => ColorBlindnessService.Simulate(Color, ColorBlindMode);

        public IBrush Brush => new SolidColorBrush(DisplayColor);

        // Picks readable black or white text/icons against whatever is
        // actually being displayed (post color-blind simulation), using the
        // standard relative-luminance formula.
        public IBrush ForegroundBrush =>
            (0.2126 * DisplayColor.R + 0.7152 * DisplayColor.G + 0.0722 * DisplayColor.B) / 255.0 > 0.6
                ? new SolidColorBrush(Color.FromRgb(0x1E, 0x20, 0x25))
                : Brushes.White;

        public ICommand? CopyCommand { get; set; }
        public ICommand? RemoveCommand { get; set; }
        public ICommand? MoveUpCommand { get; set; }
        public ICommand? MoveDownCommand { get; set; }

        partial void OnColorChanged(Color value)
        {
            OnPropertyChanged(nameof(Hex));
            OnPropertyChanged(nameof(DisplayColor));
            OnPropertyChanged(nameof(Brush));
            OnPropertyChanged(nameof(ForegroundBrush));
        }

        partial void OnColorBlindModeChanged(ColorBlindMode value)
        {
            OnPropertyChanged(nameof(DisplayColor));
            OnPropertyChanged(nameof(Brush));
            OnPropertyChanged(nameof(ForegroundBrush));
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