using Avalonia.Media;
using Spectrum.Models;
using System.Collections.Generic;
using System.Linq;

namespace Spectrum.Services
{
    /// <summary>A pair of palette colors that becomes hard to tell apart under a given
    /// simulated color-vision deficiency, with the ΔE distance that proves it.</summary>
    public sealed record VisionCollision(
        ColorBlindMode Mode,
        string FirstName,
        string SecondName,
        Color FirstColor,
        Color SecondColor,
        double DeltaE);

    /// <summary>Instead of only *showing* the palette under simulated color vision
    /// (which asks the user to spot problems by eye), this computes them: every
    /// palette pair is simulated under each CVD form and measured with CIE76 ΔE.
    /// Pairs that collapse below the threshold are called out by name so the
    /// designer knows exactly which two colors need separating — the \"pinch
    /// point\" model colors.codes uses on its vision page.</summary>
    public static class VisionCollisionService
    {
        /// <summary>ΔE below this after simulation reads as \"the same color\" for
        /// UI purposes (CIE76: &lt;2.3 is imperceptible, &lt;10 is very hard to split).</summary>
        public const double CollisionThreshold = 12.0;

        private static readonly ColorBlindMode[] SimulatedModes =
        {
            ColorBlindMode.Protanopia,
            ColorBlindMode.Deuteranopia,
            ColorBlindMode.Tritanopia,
            ColorBlindMode.Achromatopsia,
        };

        /// <summary>Finds every colliding pair in the palette, worst first. Names come
        /// from the swatches themselves; a pair is reported once per CVD form since
        /// the fix (a non-color signal) is the same.</summary>
        public static IReadOnlyList<VisionCollision> FindCollisions(
            IReadOnlyList<(string Name, Color Color)> palette)
        {
            var collisions = new List<VisionCollision>();

            if (palette.Count < 2) return collisions;

            foreach (var mode in SimulatedModes)
            {
                for (var i = 0; i < palette.Count; i++)
                {
                    for (var j = i + 1; j < palette.Count; j++)
                    {
                        var a = ColorBlindnessService.Simulate(palette[i].Color, mode);
                        var b = ColorBlindnessService.Simulate(palette[j].Color, mode);
                        var delta = ColorMathService.DeltaE(a, b);

                        if (delta < CollisionThreshold)
                        {
                            collisions.Add(new VisionCollision(
                                mode,
                                palette[i].Name,
                                palette[j].Name,
                                palette[i].Color,
                                palette[j].Color,
                                delta));
                        }
                    }
                }
            }

            return collisions
                .OrderBy(c => c.DeltaE)
                .ThenBy(c => c.Mode)
                .ToList();
        }
    }
}
