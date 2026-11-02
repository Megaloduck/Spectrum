using Spectrum.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spectrum.Services
{
    /// <summary>One role in the prompt's role map: the semantic name, the CSS custom
    /// property it becomes, what it paints in the scenes, and the resolved hex.</summary>
    public sealed record PromptRole(string Name, string Hint, string Hex)
    {
        /// <summary>CSS custom property for the role, e.g. <c>--color-primary</c>.</summary>
        public string Token => $"--color-{Slug(Name)}";

        private static string Slug(string name)
        {
            var chars = name.ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray();

            var slug = new string(chars).Trim('-');
            return string.IsNullOrWhiteSpace(slug) ? "role" : slug;
        }
    }

    /// <summary>What the Preview workspace is rendering right now, so an exported
    /// prompt describes the exact context the palette was judged in. Built by the
    /// Preview workspace — it owns the derived defaults and the color-blind
    /// simulation, the prompt just reports them.</summary>
    public sealed record PalettePromptContext(
        string PaletteName,
        string Canvas,
        string SceneTitle,
        string SceneHint,
        IReadOnlyList<PromptRole> Roles);

    /// <summary>
    /// Turns a palette into a Markdown brief that can be pasted into any AI
    /// assistant (ChatGPT, Claude, v0, Cursor) to build a UI from it: the design
    /// tokens, the role map, the source palette and the context the palette was
    /// previewed in, then a short set of ground rules.
    /// </summary>
    public static class PalettePromptService
    {
        public static string Build(IEnumerable<ColorSwatch> palette, PalettePromptContext? context = null)
        {
            var swatches = palette.ToList();
            var name = string.IsNullOrWhiteSpace(context?.PaletteName) ? "Untitled palette" : context!.PaletteName;
            var roles = context?.Roles ?? Array.Empty<PromptRole>();

            var sb = new StringBuilder();
            sb.AppendLine($"# UI prompt — “{name}”");
            sb.AppendLine();

            if (swatches.Count == 0)
            {
                sb.AppendLine("This palette has no colors yet — add a few swatches in Spectrum and export again.");
                return sb.ToString();
            }

            sb.AppendLine("Build a clean, modern interface from this palette. Treat the tokens below as the");
            sb.AppendLine("single source of truth: no new hues, and every surface, border and label uses one");
            sb.AppendLine("of these colors.");
            sb.AppendLine();

            if (roles.Count > 0)
            {
                sb.AppendLine("## Design tokens");
                sb.AppendLine();
                sb.AppendLine("```css");
                sb.AppendLine(":root {");
                foreach (var role in roles)
                    sb.AppendLine($"  {role.Token}: {role.Hex};");
                sb.AppendLine("}");
                sb.AppendLine("```");
                sb.AppendLine();

                sb.AppendLine("## Roles");
                sb.AppendLine();
                sb.AppendLine("| Token | Color | Use it for |");
                sb.AppendLine("| --- | --- | --- |");
                foreach (var role in roles)
                    sb.AppendLine($"| `{role.Token}` | `{role.Hex}` | {role.Hint} |");
                sb.AppendLine();
            }

            sb.AppendLine("## Palette");
            sb.AppendLine();
            sb.AppendLine($"“{name}” — {swatches.Count} color{(swatches.Count == 1 ? "" : "s")}");
            sb.AppendLine();
            foreach (var swatch in swatches)
                sb.AppendLine($"- {swatch.Name}: {ColorText(swatch)}");
            sb.AppendLine();

            if (roles.Count == 0)
            {
                sb.AppendLine("Assign these colors to roles yourself: use the first for primary actions and the");
                sb.AppendLine("last for accents, then derive surfaces and borders from them.");
                sb.AppendLine();
            }

            if (context is not null)
            {
                sb.AppendLine("## Context");
                sb.AppendLine();
                sb.AppendLine(ContextSentence(context));
                sb.AppendLine();
            }

            sb.AppendLine("## Ground rules");
            sb.AppendLine();
            sb.AppendLine("- Reserve the primary color for the one main action on each screen.");
            sb.AppendLine("- Cards and panels sit on the background in the surface color, separated by 1px borders.");
            sb.AppendLine("- Body and label text use the text color; secondary copy is that color at about 65% strength toward the surface.");
            sb.AppendLine("- Keep body text at WCAG AA (4.5:1) against whatever it sits on.");
            sb.AppendLine("- Use spacing, weight and borders for hierarchy before reaching for another color.");

            return sb.ToString();
        }

        /// <summary>Where the palette was previewed, in one sentence.</summary>
        private static string ContextSentence(PalettePromptContext context)
        {
            var sb = new StringBuilder("This palette was previewed");

            if (!string.IsNullOrWhiteSpace(context.Canvas))
                sb.Append($" on a {context.Canvas.Trim().ToLowerInvariant()} canvas");

            if (!string.IsNullOrWhiteSpace(context.SceneTitle))
                sb.Append($" in the {context.SceneTitle} scene");

            var description = SceneDescription(context.SceneHint);
            if (description.Length > 0)
                sb.Append($" — {char.ToLowerInvariant(description[0])}{description[1..]}");

            sb.Append('.');
            return sb.ToString();
        }

        /// <summary>The scene hint minus its reviewer-facing "check …" clause. That
        /// tail tells a human what to audit in the mockup, which makes no sense in a
        /// prompt — the rest of the hint describes the scene, which does.</summary>
        private static string SceneDescription(string hint)
        {
            if (string.IsNullOrWhiteSpace(hint)) return string.Empty;

            var text = hint.Trim();
            var lower = text.ToLowerInvariant();

            for (var index = lower.IndexOf("check", StringComparison.Ordinal);
                 index > 0;
                 index = lower.IndexOf("check", index + 1, StringComparison.Ordinal))
            {
                // Whole word only: both sides must break, so "checkbox" in a hint
                // cannot be mistaken for the audit clause.
                if (char.IsLetterOrDigit(text[index - 1])) continue;
                if (index + 5 < text.Length && char.IsLetterOrDigit(text[index + 5])) continue;

                // Whitespace is in the set on purpose: after the em dash is removed the
                // space that preceded it would otherwise survive before the full stop.
                return text[..index].Trim().TrimEnd('—', '-', ';', ',', '.', ' ', '\t');
            }

            return text;
        }

        // Translucent colors print as rgba(...) so the prompt stays valid CSS;
        // invariant culture keeps "0,50" from appearing under decimal-comma locales.
        private static string ColorText(ColorSwatch swatch) =>
            swatch.Color.A < 255
                ? FormattableString.Invariant(
                    $"rgba({swatch.Color.R}, {swatch.Color.G}, {swatch.Color.B}, {swatch.Color.A / 255.0:F2})")
                : swatch.Hex;
    }
}
