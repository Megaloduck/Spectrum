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
    /// (§2 "Paste color from clipboard").
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
                CopyFormat.Rgba => $"{c.R}, {c.G}, {c.B}, {Math.Round(alpha, 3)}",
                CopyFormat.CssRgb => c.A == 255
                    ? $"rgb({c.R}, {c.G}, {c.B})"
                    : $"rgba({c.R}, {c.G}, {c.B}, {Math.Round(alpha, 3)})",
                CopyFormat.Hsl => $"{h:N0}, {s * 100:N0}%, {l * 100:N0}%",
                CopyFormat.CssHsl => c.A == 255
                    ? $"hsl({h:N0}, {s * 100:N0}%, {l * 100:N0}%)"
                    : $"hsla({h:N0}, {s * 100:N0}%, {l * 100:N0}%, {Math.Round(alpha, 3)})",
                CopyFormat.Hsv => $"{hsv.H:N0}, {hsv.S * 100:N0}%, {hsv.V * 100:N0}%",
                CopyFormat.Lab => $"{lab.L:N1}, {lab.A:N1}, {lab.B:N1}",
                CopyFormat.Oklch => $"{ok.L:N3}, {ok.C:N3}, {ok.H:N0}",
                _ => ColorMathService.ToHex(c),
            };
        }

        /// <summary>
        /// Parses HEX, rgb(...), rgba(...), hsl(...), and bare triplets
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

        private static Color FromRgbTriplet(double[] n) =>
            Color.FromArgb(255,
                (byte)Math.Clamp(Math.Round(n[0]), 0, 255),
                (byte)Math.Clamp(Math.Round(n[1]), 0, 255),
                (byte)Math.Clamp(Math.Round(n[2]), 0, 255));

        private static Color FromHslTriplet(double[] n)
        {
            // Percentages arrive as 0-100; bare fractions (0.5) are scaled up.
            var s = n[1] <= 1 ? n[1] * 100 : n[1];
            var l = n[2] <= 1 ? n[2] * 100 : n[2];
            return ColorMathService.HslToRgb(n[0], Math.Clamp(s, 0, 100) / 100.0, Math.Clamp(l, 0, 100) / 100.0);
        }
    }
}
