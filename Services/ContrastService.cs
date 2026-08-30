using Avalonia.Media;
using System;

namespace Spectrum.Services
{
    /// <summary>
    /// WCAG 2.x relative-luminance and contrast-ratio calculations.
    /// See https://www.w3.org/TR/WCAG21/#contrast-minimum for the formulas.
    /// </summary>
    public static class ContrastService
    {
        public static double RelativeLuminance(Color color)
        {
            double LinearizeChannel(byte channel)
            {
                var srgb = channel / 255.0;
                return srgb <= 0.03928 ? srgb / 12.92 : Math.Pow((srgb + 0.055) / 1.055, 2.4);
            }

            var r = LinearizeChannel(color.R);
            var g = LinearizeChannel(color.G);
            var b = LinearizeChannel(color.B);

            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        public static double ContrastRatio(Color a, Color b)
        {
            var luminanceA = RelativeLuminance(a);
            var luminanceB = RelativeLuminance(b);

            var lighter = Math.Max(luminanceA, luminanceB);
            var darker = Math.Min(luminanceA, luminanceB);

            return (lighter + 0.05) / (darker + 0.05);
        }

        /// <summary>
        /// Returns the WCAG pass level ("AAA", "AA", or "Fail") for a given
        /// contrast ratio. Large text (18pt+/14pt+bold) has lower thresholds.
        /// </summary>
        public static string Rate(double ratio, bool largeText = false)
        {
            if (largeText)
            {
                return ratio >= 4.5 ? "AAA" : ratio >= 3.0 ? "AA" : "Fail";
            }

            return ratio >= 7.0 ? "AAA" : ratio >= 4.5 ? "AA" : "Fail";
        }
    }
}
