using Avalonia.Media;
using Spectrum.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Spectrum.Services
{
    /// <summary>Which axis an evaluation point belongs to. Design use is about the
    /// role a color can play in a UI; accessibility is about measured legibility.</summary>
    public enum EvaluationAspect
    {
        DesignUse,
        Accessibility,
    }

    /// <summary>One strength or weakness: a short headline, the reason behind it,
    /// and a priority used to order the list (higher = more important).</summary>
    public sealed record EvaluationPoint(
        EvaluationAspect Aspect,
        string Headline,
        string Reason,
        int Priority);

    /// <summary>A palette color close to the evaluated one, with its CIE76 ΔE.</summary>
    public sealed record PaletteNeighbor(string Name, Color Color, double DeltaE);

    /// <summary>The evaluation of one color in the context of its palette.
    ///
    /// Everything is derived deterministically from the existing color services
    /// (HSL/OKLCH thresholds, WCAG contrast, APCA, CVD simulation). Strength and
    /// weakness ordering is explicit, so the same color and palette always produce
    /// the same list in the same order.</summary>
    public sealed record ColorEvaluation(
        Color Color,
        string Name,
        string OneLine,
        IReadOnlyList<string> Tags,
        IReadOnlyList<EvaluationPoint> Strengths,
        IReadOnlyList<EvaluationPoint> Weaknesses,
        IReadOnlyList<VisionCollision> Collisions,
        IReadOnlyList<PaletteNeighbor> Neighbors);

    /// <summary>
    /// Turns a single color into the Analyze workspace's evaluation: what the color
    /// is, where it is strong, where it is weak, and the palette facts that prove
    /// both. This is a design-review heuristic, not a compliance audit — the
    /// accessibility points only state what the measured WCAG/APCA numbers support.
    /// </summary>
    public static class ColorEvaluationService
    {
        private static readonly Color White = Colors.White;
        private static readonly Color Black = Colors.Black;

        public static ColorEvaluation Evaluate(
            Color color,
            string name,
            IReadOnlyList<(string Name, Color Color)> palette)
        {
            var (h, s, l) = ColorMathService.RgbToHsl(color);
            var ok = ColorMathService.RgbToOklch(color);
            var tags = ColorCharacterService.Describe(color);

            var allCollisions = FindAllCollisions(palette);
            var collisions = allCollisions
                .Where(c => c.FirstColor == color || c.SecondColor == color)
                .Take(3)
                .ToArray();
            var strengths = BuildStrengths(color, h, s, l, ok, palette, allCollisions);
            var weaknesses = BuildWeaknesses(color, h, s, l, collisions);

            return new ColorEvaluation(
                color,
                name,
                DescribeOneLine(h, s, l),
                tags,
                strengths,
                weaknesses,
                collisions,
                FindNeighbors(color, name, palette));
        }

        // ---------------- One-line character ----------------

        /// <summary>"What this color is" in one sentence — the same thresholds the
        /// tags use, written as a role instead of a label.</summary>
        private static string DescribeOneLine(double h, double s, double l)
        {
            var family = HueFamily(h, s);
            var lightness = l >= 0.85 ? "very light"
                : l >= 0.70 ? "light"
                : l <= 0.15 ? "very dark"
                : l <= 0.30 ? "dark"
                : "mid";

            if (s >= 0.70 && l is > 0.30 and < 0.75)
                return $"A vivid {family} at {lightness} lightness — a natural point of focus, too strong to sit behind content.";

            if (l >= 0.85 && s <= 0.25)
                return $"A near-white {family} — reads as a surface first, as a color second.";

            if (s <= 0.25 && l <= 0.35)
                return $"A deep, quiet {family} — ink and structure, not emphasis.";

            if (s <= 0.25 && l >= 0.65)
                return $"A pale, muted {family} — calm enough to hold a large field.";

            if (l >= 0.75 && s >= 0.55)
                return $"A light, saturated {family} — cheerful as a small accent; loud over a large area.";

            if (l <= 0.30 && s >= 0.40)
                return $"A dark, saturated {family} — weighty and deliberate; better for headers than for fills.";

            var chroma = s <= 0.55 ? "soft" : "saturated";
            return $"A {chroma} {family} at {lightness} lightness — useful, but it needs a stronger neighbor to lead a hierarchy.";
        }

        // ---------------- Strengths ----------------

        private static IReadOnlyList<EvaluationPoint> BuildStrengths(
            Color color,
            double h,
            double s,
            double l,
            OklchColor ok,
            IReadOnlyList<(string Name, Color Color)> palette,
            IReadOnlyList<VisionCollision> allCollisions)
        {
            var design = new List<EvaluationPoint>();
            var accessibility = new List<EvaluationPoint>();

            // Design-use roles, strongest first within the group.
            if (s >= 0.55 && l is > 0.28 and < 0.78)
            {
                design.Add(new(EvaluationAspect.DesignUse,
                    "Accent / call-to-action",
                    $"Chroma {ok.C:N2} at lightness {ok.L:N2} makes it distinct from neutrals without turning it into a field.",
                    80));
            }

            if (s <= 0.22 && l >= 0.80)
            {
                design.Add(new(EvaluationAspect.DesignUse,
                    "Surface / background",
                    "Pale and low-chroma — content stays readable on it and it does not compete with accent colors.",
                    72));
            }

            if (s <= 0.30 && l <= 0.30)
            {
                design.Add(new(EvaluationAspect.DesignUse,
                    "Dark surface / ink",
                    "Deep and quiet — it carries structure without introducing a second chromatic voice.",
                    68));
            }

            if (s <= 0.10)
            {
                design.Add(new(EvaluationAspect.DesignUse,
                    "Neutral anchor",
                    "Almost no chroma, so it can sit beside vivid colors without adding a second point of view.",
                    60));
            }

            if (s is >= 0.20 and <= 0.65 && l is > 0.18 and < 0.62)
            {
                design.Add(new(EvaluationAspect.DesignUse,
                    "Border / fill",
                    "A mid-tone that separates edges or fills controls without the weight of a large surface.",
                    52));
            }

            // Accessibility: only measured pass results, never inferred intent.
            var ratioWhite = ContrastService.ContrastRatio(color, White);
            var ratioBlack = ContrastService.ContrastRatio(color, Black);

            if (ratioWhite >= 7.0)
            {
                accessibility.Add(new(EvaluationAspect.Accessibility,
                    "Body text on white (AAA)",
                    $"Contrast {ratioWhite:N2}:1 — clears the AAA 7:1 body-text level.",
                    100));
            }
            else if (ratioWhite >= 4.5)
            {
                accessibility.Add(new(EvaluationAspect.Accessibility,
                    "Body text on white (AA)",
                    $"Contrast {ratioWhite:N2}:1 — clears the AA 4.5:1 body-text level.",
                    92));
            }
            else if (ratioWhite >= 3.0)
            {
                accessibility.Add(new(EvaluationAspect.Accessibility,
                    "Large text / UI on white",
                    $"Contrast {ratioWhite:N2}:1 — clears 3:1 for large text and UI components, not body copy.",
                    66));
            }

            if (ratioBlack >= 7.0)
            {
                accessibility.Add(new(EvaluationAspect.Accessibility,
                    "Body text on black (AAA)",
                    $"Contrast {ratioBlack:N2}:1 — clears the AAA 7:1 body-text level.",
                    98));
            }
            else if (ratioBlack >= 4.5)
            {
                accessibility.Add(new(EvaluationAspect.Accessibility,
                    "Body text on black (AA)",
                    $"Contrast {ratioBlack:N2}:1 — clears the AA 4.5:1 body-text level.",
                    90));
            }
            else if (ratioBlack >= 3.0)
            {
                accessibility.Add(new(EvaluationAspect.Accessibility,
                    "Large text / UI on black",
                    $"Contrast {ratioBlack:N2}:1 — clears 3:1 for large text and UI components, not body copy.",
                    64));
            }

            if (palette.Count >= 2 &&
                !allCollisions.Any(c => c.FirstColor == color || c.SecondColor == color))
            {
                accessibility.Add(new(EvaluationAspect.Accessibility,
                    "Palette-distinct under simulated vision",
                    "No protan, deutan, tritan or achromatopsia pair collapses this color into a neighbor below ΔE 12.",
                    58));
            }

            var points = design.OrderByDescending(p => p.Priority).Take(4)
                .Concat(accessibility.OrderByDescending(p => p.Priority).Take(3))
                .ToList();

            return points;
        }

        // ---------------- Weaknesses ----------------

        private static IReadOnlyList<EvaluationPoint> BuildWeaknesses(
            Color color,
            double h,
            double s,
            double l,
            IReadOnlyList<VisionCollision> collisions)
        {
            var design = new List<EvaluationPoint>();
            var accessibility = new List<EvaluationPoint>();

            var ratioWhite = ContrastService.ContrastRatio(color, White);
            var ratioBlack = ContrastService.ContrastRatio(color, Black);

            if (ratioWhite < 3.0 && ratioBlack < 3.0)
            {
                accessibility.Add(new(EvaluationAspect.Accessibility,
                    "Fails text contrast on both backgrounds",
                    $"{ratioWhite:N2}:1 on white and {ratioBlack:N2}:1 on black — below the 3:1 floor for large text. Use it as a fill or beside a text-safe tone.",
                    110));
            }
            else
            {
                if (ratioWhite < 3.0)
                {
                    accessibility.Add(new(EvaluationAspect.Accessibility,
                        "Not usable as text on white",
                        $"{ratioWhite:N2}:1 — body text needs 4.5:1 and large text 3:1. Keep it on dark surfaces or behind content.",
                        106));
                }
                else if (ratioWhite < 4.5)
                {
                    accessibility.Add(new(EvaluationAspect.Accessibility,
                        "Large text only on white",
                        $"{ratioWhite:N2}:1 clears 3:1 for large text and UI, but not the 4.5:1 body-text floor.",
                        78));
                }

                if (ratioBlack < 3.0)
                {
                    accessibility.Add(new(EvaluationAspect.Accessibility,
                        "Not usable as text on black",
                        $"{ratioBlack:N2}:1 — body text needs 4.5:1 and large text 3:1. Keep it on light surfaces or behind content.",
                        104));
                }
                else if (ratioBlack < 4.5)
                {
                    accessibility.Add(new(EvaluationAspect.Accessibility,
                        "Large text only on black",
                        $"{ratioBlack:N2}:1 clears 3:1 for large text and UI, but not the 4.5:1 body-text floor.",
                        76));
                }
            }

            foreach (var collision in collisions.Take(2))
            {
                var other = collision.FirstColor == color ? collision.SecondName : collision.FirstName;
                accessibility.Add(new(EvaluationAspect.Accessibility,
                    $"Too close to {other} under {SpacedMode(collision.Mode)}",
                    $"ΔE {collision.DeltaE:N1} after simulation (below 12) — add a label, shape or spacing so the pair never carries meaning on color alone.",
                    96 - (int)Math.Round(collision.DeltaE)));
            }

            // Design-use cautions, ordered by how much they limit the color.
            if (s >= 0.65 && l is > 0.30 and < 0.80)
            {
                design.Add(new(EvaluationAspect.DesignUse,
                    "Dominates a large area",
                    "High chroma at mid lightness pulls attention; keep it to accents, indicators and small fills.",
                    70));
            }

            if (l >= 0.78 && s >= 0.55)
            {
                design.Add(new(EvaluationAspect.DesignUse,
                    "Gets loud as a large field",
                    "Light and saturated — over a big area it stops behaving like a surface; pair it with a quieter tone.",
                    62));
            }

            if (l <= 0.25 && s >= 0.40)
            {
                design.Add(new(EvaluationAspect.DesignUse,
                    "Heavy as a fill",
                    "Dark and chromatic — large areas become weighty. Better as text, borders or a deliberate dark surface.",
                    60));
            }

            if (s <= 0.10)
            {
                design.Add(new(EvaluationAspect.DesignUse,
                    "No emphasis on its own",
                    "Near-neutral — it can support a hierarchy but cannot lead one.",
                    54));
            }

            if (s is >= 0.30 and <= 0.55 && l is >= 0.40 and <= 0.65)
            {
                design.Add(new(EvaluationAspect.DesignUse,
                    "Undecided mid-tone",
                    "Mid chroma at mid lightness reads as neither accent nor neutral; give it one specific role.",
                    48));
            }

            if (l <= 0.30 && s <= 0.20)
            {
                design.Add(new(EvaluationAspect.DesignUse,
                    "Can read as disabled",
                    "Deep and muted — on controls or labels it may look inactive next to a stronger tone.",
                    46));
            }

            if (l >= 0.90 && s is > 0.10 and <= 0.35)
            {
                design.Add(new(EvaluationAspect.DesignUse,
                    "Sits between neutral and color",
                    "Light with residual chroma — depending on its neighbors it reads as a tint or as a gray.",
                    42));
            }

            return design.OrderByDescending(p => p.Priority).Take(3)
                .Concat(accessibility.OrderByDescending(p => p.Priority).Take(4))
                .ToList();
        }

        // ---------------- Palette context ----------------

        private static IReadOnlyList<VisionCollision> FindAllCollisions(
            IReadOnlyList<(string Name, Color Color)> palette)
            => palette.Count < 2
                ? Array.Empty<VisionCollision>()
                : VisionCollisionService.FindCollisions(palette);

        private static IReadOnlyList<PaletteNeighbor> FindNeighbors(
            Color color,
            string name,
            IReadOnlyList<(string Name, Color Color)> palette)
            => palette
                .Where(p => !(p.Color == color && string.Equals(p.Name, name, StringComparison.Ordinal)))
                .Select(p => new PaletteNeighbor(p.Name, p.Color, ColorMathService.DeltaE(color, p.Color)))
                .OrderBy(n => n.DeltaE)
                .Take(4)
                .ToArray();

        // ---------------- Helpers ----------------

        private static string SpacedMode(ColorBlindMode mode) => mode switch
        {
            ColorBlindMode.Protanopia => "protanopia",
            ColorBlindMode.Deuteranopia => "deuteranopia",
            ColorBlindMode.Tritanopia => "tritanopia",
            ColorBlindMode.Achromatopsia => "achromatopsia",
            _ => "simulated vision",
        };

        private static string HueFamily(double h, double s)
        {
            if (s <= 0.08)
            {
                return h >= 340 || h < 20 ? "warm gray"
                    : h < 70 ? "warm gray"
                    : h < 160 ? "green gray"
                    : h < 280 ? "blue gray"
                    : "cool gray";
            }

            return h switch
            {
                < 15 or >= 345 => "red",
                < 45 => "orange",
                < 70 => "yellow",
                < 160 => "green",
                < 200 => "cyan",
                < 255 => "blue",
                < 290 => "violet",
                < 345 => "magenta",
                _ => "color",
            };
        }
    }
}
