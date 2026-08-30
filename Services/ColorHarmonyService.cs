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
            var alpha = baseColor.A;
            var result = new List<Color> { baseColor };

            switch (type)
            {
                case HarmonyType.Complementary:
                    result.Add(FromHsl(h + 180, s, l, alpha));
                    break;

                case HarmonyType.Analogous:
                    result.Add(FromHsl(h - 30, s, l, alpha));
                    result.Add(FromHsl(h + 30, s, l, alpha));
                    result.Add(FromHsl(h - 60, s, l, alpha));
                    result.Add(FromHsl(h + 60, s, l, alpha));
                    break;

                case HarmonyType.Triadic:
                    result.Add(FromHsl(h + 120, s, l, alpha));
                    result.Add(FromHsl(h + 240, s, l, alpha));
                    break;

                case HarmonyType.SplitComplementary:
                    result.Add(FromHsl(h + 150, s, l, alpha));
                    result.Add(FromHsl(h + 210, s, l, alpha));
                    break;

                case HarmonyType.Tetradic:
                    result.Add(FromHsl(h + 90, s, l, alpha));
                    result.Add(FromHsl(h + 180, s, l, alpha));
                    result.Add(FromHsl(h + 270, s, l, alpha));
                    break;

                case HarmonyType.Monochromatic:
                    result.Clear();
                    for (var i = 0; i < 5; i++)
                    {
                        var lightness = Math.Clamp(l - 0.4 + i * 0.2, 0.05, 0.95);
                        result.Add(FromHsl(h, s, lightness, alpha));
                    }
                    break;
            }

            return result;
        }

        private static (double H, double S, double L) ToHsl(Color c)
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

        private static Color FromHsl(double h, double s, double l, byte alpha = 255)
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
                alpha,
                (byte)Math.Round(r * 255),
                (byte)Math.Round(g * 255),
                (byte)Math.Round(b * 255));
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
