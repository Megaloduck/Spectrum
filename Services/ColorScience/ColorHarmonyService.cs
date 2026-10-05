using Avalonia.Media;
using Spectrum.Models;
using System;
using System.Collections.Generic;

namespace Spectrum.Services
{
    /// <summary>
    /// Generates sets of related colors from a base color using
    /// classic color-wheel harmony rules (hue rotations on the HSL wheel).
    /// </summary>
    public static class ColorHarmonyService
    {
        public static List<Color> Generate(Color baseColor, HarmonyType type)
        {
            var (h, s, l) = ToHsl(baseColor);
            // Harmony-derived colors inherit the base color's alpha, so the
            // Opacity slider affects the whole generated set consistently.
            var a = baseColor.A;
            var result = new List<Color> { baseColor };

            switch (type)
            {
                case HarmonyType.Complementary:
                    result.Add(FromHsl(h + 180, s, l, a));
                    break;

                case HarmonyType.Analogous:
                    result.Add(FromHsl(h - 30, s, l, a));
                    result.Add(FromHsl(h + 30, s, l, a));
                    result.Add(FromHsl(h - 60, s, l, a));
                    result.Add(FromHsl(h + 60, s, l, a));
                    break;

                case HarmonyType.Triadic:
                    result.Add(FromHsl(h + 120, s, l, a));
                    result.Add(FromHsl(h + 240, s, l, a));
                    break;

                case HarmonyType.SplitComplementary:
                    result.Add(FromHsl(h + 150, s, l, a));
                    result.Add(FromHsl(h + 210, s, l, a));
                    break;

                case HarmonyType.Tetradic:
                    result.Add(FromHsl(h + 90, s, l, a));
                    result.Add(FromHsl(h + 180, s, l, a));
                    result.Add(FromHsl(h + 270, s, l, a));
                    break;

                case HarmonyType.Monochromatic:
                    result.Clear();
                    for (var i = 0; i < 5; i++)
                    {
                        var lightness = Math.Clamp(l - 0.4 + i * 0.2, 0.05, 0.95);
                        result.Add(FromHsl(h, s, lightness, a));
                    }
                    break;

                case HarmonyType.Random:
                    // No rule to apply — caller falls back to independent
                    // random colors (see MainWindowViewModel.GeneratePalette).
                    break;
            }

            return result;
        }

        /// <summary>
        /// Returns a single random color that stays within pleasant saturation/lightness
        /// bands (vivid without being oversaturated, and never near-black or near-white).
        /// Used by the Coolors-style "press Space to generate" palette generator, where
        /// each unlocked swatch is rerolled independently rather than derived from one
        /// base color + harmony rule. Always fully opaque — it isn't derived from the
        /// base color, so there's no alpha value to inherit.
        /// </summary>
        public static Color RandomPleasant(Random rng)
        {
            double h = rng.NextDouble() * 360.0;
            double s = 0.45 + rng.NextDouble() * 0.45; // 45%–90% saturation
            double l = 0.30 + rng.NextDouble() * 0.45; // 30%–75% lightness
            return FromHsl(h, s, l);
        }

        /// <summary>Converts an RGB color to (Hue 0-360, Saturation 0-1, Lightness 0-1).</summary>
        public static (double H, double S, double L) ToHsl(Color c)
        {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double h;
            double l = (max + min) / 2.0;
            double s;

            if (Math.Abs(max - min) < 0.00001)
            {
                h = 0;
                s = 0;
            }
            else
            {
                double d = max - min;
                s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

                if (max == r)
                    h = (g - b) / d + (g < b ? 6 : 0);
                else if (max == g)
                    h = (b - r) / d + 2;
                else
                    h = (r - g) / d + 4;

                h *= 60;
            }

            return (h, s, l);
        }

        /// <summary>
        /// Converts (Hue 0-360, Saturation 0-1, Lightness 0-1) back to an RGB color.
        /// <paramref name="a"/> defaults to fully opaque so existing call sites that
        /// don't care about alpha don't need to change.
        /// </summary>
        public static Color FromHsl(double h, double s, double l, byte a = 255)
        {
            h = ((h % 360) + 360) % 360;
            double r, g, b;

            if (s <= 0.00001)
            {
                r = g = b = l;
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = HueToRgb(p, q, h / 360.0 + 1.0 / 3.0);
                g = HueToRgb(p, q, h / 360.0);
                b = HueToRgb(p, q, h / 360.0 - 1.0 / 3.0);
            }

            return new Color(
                a,
                (byte)Math.Round(Math.Clamp(r, 0, 1) * 255),
                (byte)Math.Round(Math.Clamp(g, 0, 1) * 255),
                (byte)Math.Round(Math.Clamp(b, 0, 1) * 255));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }
    }
}   
