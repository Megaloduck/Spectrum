using Avalonia.Media;
using Spectrum.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Spectrum.Services
{
    /// <summary>
    /// Formats a color for the clipboard in every notation the sheet asks for
    /// (§2 "Copy color in multiple formats") and parses any of them back
    /// (§2 "Paste color from clipboard"). All numeric formatting is
    /// culture-invariant: the output is clipboard text and CSS that must read
    /// identically on every machine, and TryParse reads numbers back with
    /// InvariantCulture — a decimal-comma locale would otherwise produce
    /// strings this class can't parse (and invalid CSS).
    /// </summary>
    public static class ColorFormatService
    {
        private static readonly Regex NumberRegex = new(@"-?\d+(?:\.\d+)?", RegexOptions.Compiled);

        public static string Format(Color c, CopyFormat format)
        {
            var alpha = c.A / 255.0;
            var (h, s, l) = ColorMathService.RgbToHsl(c);
            var hsv = ColorMathService.RgbToHsv(c);
            var lab = ColorMathService.RgbToLab(c);
            var ok = ColorMathService.RgbToOklch(c);

            return format switch
            {
                CopyFormat.Hex => c.A == 255
                    ? ColorMathService.ToHex(c)
                    : $"{ColorMathService.ToHex(c)}{c.A:X2}",
                CopyFormat.Rgb => $"{c.R}, {c.G}, {c.B}",
                CopyFormat.Rgba => Invariant($"{c.R}, {c.G}, {c.B}, {Math.Round(alpha, 3)}"),
                CopyFormat.CssRgb => c.A == 255
                    ? $"rgb({c.R}, {c.G}, {c.B})"
                    : Invariant($"rgba({c.R}, {c.G}, {c.B}, {Math.Round(alpha, 3)})"),
                CopyFormat.Hsl => Invariant($"{h:N0}, {s * 100:N0}%, {l * 100:N0}%"),
                CopyFormat.CssHsl => c.A == 255
                    ? Invariant($"hsl({h:N0}, {s * 100:N0}%, {l * 100:N0}%)")
                    : Invariant($"hsla({h:N0}, {s * 100:N0}%, {l * 100:N0}%, {Math.Round(alpha, 3)})"),
                // Prefixed notations: they make every copy format round-trip
                // back through TryParse below, and lab()/oklch()/hsv() are
                // accepted by CSS and most design tools as-is. Bare numeric
                // triplets would be ambiguous with RGB/HSL on the way back in.
                CopyFormat.Hsv => Invariant($"hsv({hsv.H:N0}, {hsv.S * 100:N0}%, {hsv.V * 100:N0}%)"),
                CopyFormat.Lab => Invariant($"lab({lab.L:N1}, {lab.A:N1}, {lab.B:N1})"),
                CopyFormat.Oklch => Invariant($"oklch({ok.L:N3}, {ok.C:N3}, {ok.H:N0})"),
                _ => ColorMathService.ToHex(c),
            };
        }

        private static string Invariant(FormattableString text) =>
            text.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Parses HEX, rgb(...), rgba(...) (alpha honoured), hsl(...), hsv(...),
        /// lab(...), lch(...), oklch(...), and bare triplets
        /// ("255, 128, 0" is read as RGB; "210, 60%, 50%" as HSL).
        /// </summary>
        public static bool TryParse(string? text, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var input = text.Trim();

            if (input.StartsWith('#') || input.All(Uri.IsHexDigit) && input.Length is 3 or 6 or 8)
            {
                if (ColorMathService.TryParseHex(input, out color)) return true;
            }

            var lower = input.ToLowerInvariant();
            var numbers = NumberRegex.Matches(input)
                .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture))
                .ToArray();

            if (numbers.Length < 3) return false;

            // Prefixed notations, longest match first so "oklch(" isn't
            // mistaken for "lch(". These used to fall through to the bare
            // triplet path and silently decode as RGB garbage.
            if (lower.Contains("oklch("))
            {
                color = ColorMathService.OklchToRgb(new OklchColor(numbers[0], numbers[1], numbers[2]));
                return true;
            }

            if (lower.Contains("lab("))
            {
                color = ColorMathService.LabToRgb(new LabColor(numbers[0], numbers[1], numbers[2]));
                return true;
            }

            if (lower.Contains("lch("))
            {
                color = ColorMathService.LabToRgb(
                    ColorMathService.LchToLab(new LchColor(numbers[0], numbers[1], numbers[2])));
                return true;
            }

            if (lower.Contains("hsv(") || lower.Contains("hsb("))
            {
                var rgb = ColorMathService.HsvToRgb(
                    new HsvColor(numbers[0], Percent(numbers[1]), Percent(numbers[2])));
                color = WithAlpha(rgb, numbers);
                return true;
            }

            if (lower.Contains("hsl"))
            {
                color = FromHslTriplet(numbers);
                return true;
            }

            if (lower.Contains("rgb") || input.Contains('%'))
            {
                if (input.Contains('%'))
                {
                    color = FromHslTriplet(numbers);
                    return true;
                }

                color = FromRgbTriplet(numbers);
                return true;
            }

            // Bare triplet → RGB (hsl is only assumed with an "hsl" prefix or %).
            color = FromRgbTriplet(numbers);
            return true;
        }

        /// <summary>0.5 means 50% and 50 means 50% — both spellings are common.</summary>
        private static double Percent(double value) => value <= 1 ? value : value / 100.0;

        /// <summary>Applies a 4th triplet number as alpha when present
        /// (0..1 fractions and 0..255 byte values are both accepted).</summary>
        private static Color WithAlpha(Color rgb, double[] numbers) =>
            numbers.Length >= 4
                ? Color.FromArgb(AlphaFrom(numbers[3]), rgb.R, rgb.G, rgb.B)
                : rgb;

        private static byte AlphaFrom(double value) =>
            (byte)Math.Clamp(Math.Round(value <= 1 ? value * 255 : value), 0, 255);

        private static Color FromRgbTriplet(double[] n)
        {
            var rgb = Color.FromArgb(255,
                (byte)Math.Clamp(Math.Round(n[0]), 0, 255),
                (byte)Math.Clamp(Math.Round(n[1]), 0, 255),
                (byte)Math.Clamp(Math.Round(n[2]), 0, 255));
            return WithAlpha(rgb, n);
        }

        private static Color FromHslTriplet(double[] n)
        {
            // Percentages arrive as 0-100; bare fractions (0.5) are scaled up.
            var s = n[1] <= 1 ? n[1] * 100 : n[1];
            var l = n[2] <= 1 ? n[2] * 100 : n[2];
            var rgb = ColorMathService.HslToRgb(n[0], Math.Clamp(s, 0, 100) / 100.0, Math.Clamp(l, 0, 100) / 100.0);
            return WithAlpha(rgb, n);
        }
    }
}
