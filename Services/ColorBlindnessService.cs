using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia.Media;
using Spectrum.Models;

namespace Spectrum.Services
{
    /// <summary>
    /// Approximates how a color would look to someone with a given form of
    /// color blindness, using widely-used simplified sRGB simulation
    /// matrices. This is meant as a quick design-time preview — for
    /// anything accessibility-critical, verify with a dedicated tool.
    /// </summary>
    public static class ColorBlindnessService
    {
        public static Color Simulate(Color color, ColorBlindMode mode)
        {
            if (mode == ColorBlindMode.None) return color;

            double r = color.R;
            double g = color.G;
            double b = color.B;
            double nr, ng, nb;

            switch (mode)
            {
                case ColorBlindMode.Protanopia:
                    nr = 0.567 * r + 0.433 * g;
                    ng = 0.558 * r + 0.442 * g;
                    nb = 0.242 * g + 0.758 * b;
                    break;

                case ColorBlindMode.Deuteranopia:
                    nr = 0.625 * r + 0.375 * g;
                    ng = 0.700 * r + 0.300 * g;
                    nb = 0.300 * g + 0.700 * b;
                    break;

                case ColorBlindMode.Tritanopia:
                    nr = 0.950 * r + 0.050 * g;
                    ng = 0.433 * g + 0.567 * b;
                    nb = 0.475 * g + 0.525 * b;
                    break;

                case ColorBlindMode.Achromatopsia:                  
                    double luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                    nr = luminance;
                    ng = luminance;
                    nb = luminance;
                    break;

                default:
                    return color;
            }

            // Preserve the original alpha — previously this used Color.FromRgb,
            // which silently forced every simulated color to fully opaque.
            return Color.FromArgb(color.A, Clamp(nr), Clamp(ng), Clamp(nb));
        }

        private static byte Clamp(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);
    }
}
