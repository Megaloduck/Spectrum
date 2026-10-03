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

        /// <summary>Rich tooltip readouts (§7 "Tooltips with color info").</summary>
        public string RgbLabel =>
            Color.A == 255
                ? $"rgb({Color.R}, {Color.G}, {Color.B})"
                : $"rgba({Color.R}, {Color.G}, {Color.B}, {System.Math.Round(Color.A / 255.0, 2)})";

        public string HslLabel
        {
            get
            {
                var (h, s, l) = ColorHarmonyService.ToHsl(Color);
                return $"hsl({h:N0}, {s * 100:N0}%, {l * 100:N0}%)";
            }
        }

        public string SimulatedNote =>
            ColorBlindMode == ColorBlindMode.None
                ? string.Empty
                : $" shown as {ColorBlindMode}";

        // ---------------- §8 "Contrast overlay on swatches" ----------------

        [ObservableProperty]
        private bool _showContrastOverlay;

        public string WhiteTextBadge
        {
            get
            {
                var ratio = ContrastService.ContrastRatio(DisplayColor, Colors.White);
                var lc = ContrastService.Apca(Colors.White, DisplayColor);
                return $"Aa white {ContrastService.Rate(ratio)} · {ratio:N1}:1 · Lc {lc:N0}";
            }
        }

        public string BlackTextBadge
        {
            get
            {
                var ratio = ContrastService.ContrastRatio(DisplayColor, Colors.Black);
                var lc = ContrastService.Apca(Colors.Black, DisplayColor);
                return $"Aa black {ContrastService.Rate(ratio)} · {ratio:N1}:1 · Lc {lc:N0}";
            }
        }

        // ---------------- §8 "Swatch zoom view" readouts ----------------

        public string LabLabel
        {
            get
            {
                var lab = ColorMathService.RgbToLab(Color);
                var lch = ColorMathService.LabToLch(lab);
                return $"LAB {lab.L:N1}, {lab.A:N1}, {lab.B:N1} · LCH {lch.L:N1}, {lch.C:N1}, {lch.H:N0}°";
            }
        }

        public string OklchLabel
        {
            get
            {
                var ok = ColorMathService.RgbToOklch(Color);
                return $"OKLCH {ok.L:N3}, {ok.C:N3}, {ok.H:N0}°";
            }
        }

        public Color DisplayColor => ColorBlindnessService.Simulate(Color, ColorBlindMode);

        public IBrush Brush => new SolidColorBrush(DisplayColor);

        // Picks readable black or white text/icons against whatever is
        // actually being displayed (post color-blind simulation), using the
        // standard relative-luminance formula.
        private Color ForegroundColor =>
            (0.2126 * DisplayColor.R + 0.7152 * DisplayColor.G + 0.0722 * DisplayColor.B) / 255.0 > 0.6
                ? Color.FromRgb(0x1E, 0x20, 0x25)
                : Colors.White;

        public IBrush ForegroundBrush => new SolidColorBrush(ForegroundColor);

        // WCAG 2.x contrast of this card's own label text (ForegroundColor)
        // against whatever color is actually displayed — lets each swatch
        // flag whether its own Name/Hex text would actually be legible,
        // including under color-blind simulation.
        public double ContrastRatio => ContrastService.ContrastRatio(DisplayColor, ForegroundColor);

        public string ContrastRating => ContrastService.Rate(ContrastRatio);

        public string ContrastLabel => $"{ContrastRatio:N1}:1 · {ContrastRating}";

        public ICommand? CopyCommand { get; set; }
        public ICommand? RemoveCommand { get; set; }
        public ICommand? MoveUpCommand { get; set; }
        public ICommand? MoveDownCommand { get; set; }

        /// <summary>Opens the spectrum picker for this swatch (wired by the owning ViewModel).</summary>
        public ICommand? EditColorCommand { get; set; }

        /// <summary>Duplicates this swatch next to itself (wired by the owning ViewModel).</summary>
        public ICommand? DuplicateCommand { get; set; }

        partial void OnColorChanged(Color value)
        {
            OnPropertyChanged(nameof(Hex));
            OnPropertyChanged(nameof(RgbLabel));
            OnPropertyChanged(nameof(HslLabel));
            OnPropertyChanged(nameof(SimulatedNote));
            OnPropertyChanged(nameof(WhiteTextBadge));
            OnPropertyChanged(nameof(BlackTextBadge));
            OnPropertyChanged(nameof(LabLabel));
            OnPropertyChanged(nameof(OklchLabel));
            OnPropertyChanged(nameof(DisplayColor));
            OnPropertyChanged(nameof(Brush));
            OnPropertyChanged(nameof(ForegroundBrush));
            OnPropertyChanged(nameof(ContrastRatio));
            OnPropertyChanged(nameof(ContrastRating));
            OnPropertyChanged(nameof(ContrastLabel));
        }

        partial void OnColorBlindModeChanged(ColorBlindMode value)
        {
            OnPropertyChanged(nameof(DisplayColor));
            OnPropertyChanged(nameof(Brush));
            OnPropertyChanged(nameof(ForegroundBrush));
            OnPropertyChanged(nameof(ContrastRatio));
            OnPropertyChanged(nameof(ContrastRating));
            OnPropertyChanged(nameof(ContrastLabel));
            OnPropertyChanged(nameof(WhiteTextBadge));
            OnPropertyChanged(nameof(BlackTextBadge));
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
