using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Spectrum.Services;
using HsvColor = Spectrum.Services.HsvColor;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Spectrum.ViewModels
{
    /// <summary>
    /// Backs Views/ColorPickerDialog: a saturation/value spectrum plane plus
    /// hue + opacity sliders, with manual input in HEX, RGB, HSL and HSV
    /// (§2 "Edit swatch color (picker)", "Manual hex input", "RGB / HSL / HSV input",
    /// "Alpha / opacity control", "Inline large preview").
    /// </summary>
    public partial class ColorPickerViewModel : ViewModelBase
    {
        [ObservableProperty] private double _hue;        // 0..360
        [ObservableProperty] private double _saturation; // 0..100
        [ObservableProperty] private double _value;      // 0..100 (brightness)
        [ObservableProperty] private double _alpha;      // 0..100 (%)

        [ObservableProperty] private string _inputFeedback = string.Empty;

        public ColorPickerViewModel(Color initial)
        {
            var hsv = ColorMathService.RgbToHsv(initial);
            _hue = hsv.H;
            _saturation = hsv.S * 100;
            _value = hsv.V * 100;
            _alpha = initial.A / 255.0 * 100;
        }

        /// <summary>The picked color including alpha.</summary>
        public Color CurrentColor =>
            ColorMathService.HsvToRgb(new HsvColor(Hue, Saturation / 100.0, Value / 100.0),
                (byte)Math.Round(Alpha / 100.0 * 255));

        /// <summary>The picked color, fully opaque — used for previewing hue/sat/value without alpha wash.</summary>
        public Color OpaqueColor =>
            ColorMathService.HsvToRgb(new HsvColor(Hue, Saturation / 100.0, Value / 100.0));

        public IBrush PreviewBrush => new SolidColorBrush(OpaqueColor);

        /// <summary>Spectrum plane background: white → current hue (the black gradient is a second layer).</summary>
        public IBrush PlaneHueBrush => new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Colors.White, 0),
                new GradientStop(OpaqueColor, 1),
            },
        };

        /// <summary>Opacity slider track: transparent → current color.</summary>
        public IBrush AlphaTrackBrush => new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, OpaqueColor.R, OpaqueColor.G, OpaqueColor.B), 0),
                new GradientStop(OpaqueColor, 1),
            },
        };

        public string Hex => ColorMathService.ToHex(OpaqueColor);

        public string Name => ColorNamingService.GetClosestName(OpaqueColor);

        public string AlphaLabel => $"{Alpha:N0}%";

        public string RgbText
        {
            get
            {
                var c = OpaqueColor;
                var a = Alpha / 100.0;
                return a >= 0.999
                    ? $"rgb({c.R}, {c.G}, {c.B})"
                    : $"rgba({c.R}, {c.G}, {c.B}, {Math.Round(a, 2)})";
            }
        }

        public string HslText
        {
            get
            {
                var (h, s, l) = ColorMathService.RgbToHsl(OpaqueColor);
                return $"hsl({h:N0}, {s * 100:N0}%, {l * 100:N0}%)";
            }
        }

        public string HsvText
        {
            get
            {
                var hsv = ColorMathService.RgbToHsv(OpaqueColor);
                return $"hsv({hsv.H:N0}, {hsv.S * 100:N0}%, {hsv.V * 100:N0}%)";
            }
        }

        public string LabText
        {
            get
            {
                var lab = ColorMathService.RgbToLab(OpaqueColor);
                var lch = ColorMathService.LabToLch(lab);
                return $"LAB({lab.L:N1}, {lab.A:N1}, {lab.B:N1}) · LCH({lch.L:N1}, {lch.C:N1}, {lch.H:N0}°)";
            }
        }

        public string OklchText
        {
            get
            {
                var ok = ColorMathService.RgbToOklch(OpaqueColor);
                return $"OKLCH({ok.L:N3}, {ok.C:N3}, {ok.H:N0}°)";
            }
        }

        public IBrush ForegroundBrush =>
            (0.2126 * OpaqueColor.R + 0.7152 * OpaqueColor.G + 0.0722 * OpaqueColor.B) / 255.0 > 0.6
                ? new SolidColorBrush(Color.FromRgb(0x1E, 0x20, 0x25))
                : Brushes.White;

        partial void OnHueChanged(double value) => RaiseAll();
        partial void OnSaturationChanged(double value) => RaiseAll();
        partial void OnValueChanged(double value) => RaiseAll();
        partial void OnAlphaChanged(double value) => RaiseAll();

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(CurrentColor));
            OnPropertyChanged(nameof(OpaqueColor));
            OnPropertyChanged(nameof(PreviewBrush));
            OnPropertyChanged(nameof(PlaneHueBrush));
            OnPropertyChanged(nameof(AlphaTrackBrush));
            OnPropertyChanged(nameof(Hex));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(AlphaLabel));
            OnPropertyChanged(nameof(RgbText));
            OnPropertyChanged(nameof(HslText));
            OnPropertyChanged(nameof(HsvText));
            OnPropertyChanged(nameof(LabText));
            OnPropertyChanged(nameof(OklchText));
            OnPropertyChanged(nameof(ForegroundBrush));
            ColorChanged?.Invoke();
        }

        /// <summary>Raised after any slider/commit change (dialog repositions the plane marker).</summary>
        public event Action? ColorChanged;

        // ---------------- Manual inputs (commit from the dialog on Enter / focus loss) ----------------

        private static readonly Regex NumberRegex = new(@"-?\d+(?:\.\d+)?", RegexOptions.Compiled);

        public bool TryApplyHex(string text)
        {
            if (!ColorMathService.TryParseHex(text, out var color)) return Feedback("Couldn't parse that hex value.");
            ApplyColor(color);

            // Only an 8-digit input (RRGGBBAA) carries meaningful alpha.
            if (text.Trim().TrimStart('#').Length == 8)
                Alpha = color.A / 255.0 * 100;

            return true;
        }

        public bool TryApplyRgb(string text) => ApplyTriplet(text, 0, 255, "RGB", parts =>
        {
            var a = OpaqueColor.A;
            ApplyColor(Color.FromArgb(a, ToByte(parts[0]), ToByte(parts[1]), ToByte(parts[2])));
        });

        public bool TryApplyHsl(string text) => ApplyTriplet(text, 0, 360, "HSL", parts =>
        {
            // Values arrive as (hue 0-360, sat %, lightness %) as displayed.
            var a = OpaqueColor.A;
            var color = ColorMathService.HslToRgb(parts[0], Math.Clamp(parts[1], 0, 100) / 100.0,
                Math.Clamp(parts[2], 0, 100) / 100.0, a);
            ApplyColor(color);
        });

        public bool TryApplyHsv(string text) => ApplyTriplet(text, 0, 360, "HSV", parts =>
        {
            var a = OpaqueColor.A;
            var color = ColorMathService.HsvToRgb(
                new HsvColor(parts[0], Math.Clamp(parts[1], 0, 100) / 100.0, Math.Clamp(parts[2], 0, 100) / 100.0), a);
            ApplyColor(color);
        });

        private bool ApplyTriplet(string text, double min, double max, string label, Action<double[]> apply)
        {
            var numbers = NumberRegex.Matches(text ?? string.Empty)
                .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture))
                .ToArray();

            if (numbers.Length < 3)
                return Feedback($"Enter {label} as three numbers, e.g. {(label == "RGB" ? "255, 128, 0" : "210, 60%, 50%")}.");

            apply(numbers);
            return true;
        }

        /// <summary>Applies a picked color to the plane state (keeps the alpha slider as-is).</summary>
        public void ApplyColor(Color color)
        {
            var hsv = ColorMathService.RgbToHsv(color);
            Hue = hsv.H;
            Saturation = hsv.S * 100;
            Value = hsv.V * 100;
        }

        private bool Feedback(string message)
        {
            InputFeedback = message;
            return false;
        }

        private static byte ToByte(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);
    }
}
