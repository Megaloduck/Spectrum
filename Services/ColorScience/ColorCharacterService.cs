using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace Spectrum.Services
{
    /// <summary>The qualitative \"character\" of a color: tags like Pastel / Vivid /
    /// Warm and a one-line usage note, all derived deterministically from the
    /// color's HSL + OKLCH values. This is the identity-first layer a reference
    /// record needs above its raw numbers (inspired by colors.codes color pages):
    /// same input, same output, no network, no randomness.</summary>
    public static class ColorCharacterService
    {
        /// <summary>Builds the tag list for a color: lightness class, saturation class,
        /// temperature and hue family — e.g. ("Pastel", "Warm", "Orange").</summary>
        public static IReadOnlyList<string> Describe(Color c)
        {
            var (h, s, l) = ColorMathService.RgbToHsl(c);
            var tags = new List<string>(4);

            var lightness = LightnessTag(l, s);
            if (lightness is not null) tags.Add(lightness);

            var chroma = ChromaTag(s);
            if (chroma is not null) tags.Add(chroma);

            var temperature = TemperatureTag(h, s);
            if (temperature is not null) tags.Add(temperature);

            var family = HueFamily(h, s);
            if (family is not null) tags.Add(family);

            return tags;
        }

        /// <summary>A one-line, deterministic usage note for the color. Where a color
        /// sits in the lightness/chroma space suggests what it's good at: pale
        /// colors read as surfaces, deep colors as text, vivid ones as accents.</summary>
        public static string Note(Color c)
        {
            var (h, s, l) = ColorMathService.RgbToHsl(c);
            var family = HueFamily(h, s)?.ToLowerInvariant() ?? "neutral";

            if (l >= 0.93)
                return $"Nearly white with a {family} cast — best as a page or card background, never as text.";
            if (l <= 0.07)
                return $"Nearly black with a {family} cast — reads as text on light surfaces; too heavy for large fills.";

            var vivid = s >= 0.70 && l is > 0.30 and < 0.75;
            var muted = s <= 0.25;

            if (vivid)
                return $"A vivid {family} — strongest as an accent or call-to-action; check body-text contrast before using it on large areas.";
            if (muted && l >= 0.65)
                return $"A soft, muted {family} — calm enough for large surfaces and cards behind content.";
            if (muted && l <= 0.35)
                return $"A deep, muted {family} — works as a dark-mode surface or as text on light backgrounds.";
            if (muted)
                return $"A restrained {family} — flexible mid-tone that rarely fights neighboring colors.";
            if (l >= 0.75)
                return $"A light, saturated {family} — cheerful fill color; keep text on it dark enough to pass contrast.";
            if (l <= 0.25)
                return $"A dark, saturated {family} — rich and weighty; good for headers and hero sections.";

            return $"A balanced {family} — a solid mid-tone for buttons, links and highlights.";
        }

        // ---------------- tag helpers ----------------

        private static string? LightnessTag(double l, double s) => l switch
        {
            >= 0.93 => "Near-white",
            >= 0.80 => s < 0.15 ? "Pale" : "Pastel",
            >= 0.65 => "Light",
            <= 0.07 => "Near-black",
            <= 0.22 => "Deep",
            <= 0.38 => "Dark",
            _ => null, // mid-range: the other tags carry the description
        };

        private static string? ChromaTag(double s) => s switch
        {
            <= 0.06 => "Neutral",
            <= 0.25 => "Muted",
            <= 0.55 => "Soft",
            <= 0.85 => "Saturated",
            _ => "Vivid",
        };

        /// <summary>Warm / cool from the hue wheel, softened by saturation — a
        /// low-saturation color is \"temperature-quiet\" and gets no temperature tag.</summary>
        private static string? TemperatureTag(double h, double s)
        {
            if (s <= 0.12) return null;

            // Red-orange through yellow = warm; cyan through violet = cool;
            // green (70–160) sits on the fence and gets no temperature tag.
            if (h is < 70 or >= 320) return "Warm";
            return h >= 160 ? "Cool" : null;
        }

        private static string? HueFamily(double h, double s)
        {
            if (s <= 0.08)
                return h >= 340 || h < 20 ? "warm gray" : h < 70 ? "warm gray" : h < 160 ? "green gray"
                    : h < 280 ? "blue gray" : "cool gray";

            return h switch
            {
                < 15 or >= 345 => "Red",
                < 45 => "Orange",
                < 70 => "Yellow",
                < 160 => "Green",
                < 200 => "Cyan",
                < 255 => "Blue",
                < 290 => "Violet",
                < 345 => "Magenta",
                _ => null,
            };
        }
    }
}
