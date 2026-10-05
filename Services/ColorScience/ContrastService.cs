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
        /// APCA (Advanced Perceptual Contrast Algorithm, 0.98G-4g reduction).
        /// Returns the lightness contrast Lc: positive = dark text on light
        /// background, negative = light text on dark background, 0 = failure.
        /// See https://www.w3.org/WAI/GL/task-forces/silver/wiki/User:Myndex/APCA_model
        /// </summary>
        public static double Apca(Color text, Color background)
        {
            var yText = LuminanceApca(text);
            var yBg = LuminanceApca(background);

            // Soft clamp the darker of the two just above the black floor.
            const double blackThreshold = 0.022;
            if (yText < yBg)
            {
                if (yText < blackThreshold) yText = Math.Pow(blackThreshold - yText, 1.414) + yText;
            }
            else
            {
                if (yBg < blackThreshold) yBg = Math.Pow(blackThreshold - yBg, 1.414) + yBg;
            }

            const double scale = 1.14;

            if (yText < yBg)
            {
                // Dark text on a light background → positive Lc.
                var positive = (Math.Pow(yBg, 0.56) - Math.Pow(yText, 0.57)) * scale;
                return positive < 0.1 ? 0 : positive * 100 - 2.7;
            }
            else
            {
                // Light text on a dark background → negative Lc.
                var negative = (Math.Pow(yBg, 0.65) - Math.Pow(yText, 0.62)) * scale;
                return negative > -0.1 ? 0 : negative * 100 + 2.7;
            }
        }

        /// <summary>APCA's pure ^2.4 sRGB luminance (deliberately not the WCAG linearization).</summary>
        private static double LuminanceApca(Color color)
        {
            static double Pow(byte channel)
            {
                var v = channel / 255.0;
                return Math.Pow(v, 2.4);
            }

            return 0.2126 * Pow(color.R) + 0.7152 * Pow(color.G) + 0.0722 * Pow(color.B);
        }

        /// <summary>
        /// APCA verdict for UI decisions. Lc 60 ≈ WCAG 4.5:1, Lc 75 ≈ 7:1,
        /// Lc 45 ≈ 3:1 (see the W3C Silver comparison table).
        /// </summary>
        public static string RateApca(double lc)
        {
            var magnitude = Math.Abs(lc);
            if (magnitude >= 75) return "AAA";
            if (magnitude >= 60) return "AA";
            if (magnitude >= 45) return "AA Large";
            return "Fail";
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
