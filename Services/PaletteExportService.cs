using Avalonia.Media;
using Spectrum.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Spectrum.Services
{
    public enum ExportFormat
    {
        Json,
        Css,
        Scss,
        Less,
        Tailwind,
        Svg,
        Gpl,
        Ase,
        Aco,
        PlainText
    }

    /// <summary>
    /// Turns a palette into shareable text in a few common formats.
    /// </summary>
    public static class PaletteExportService
    {
        public static string Export(IEnumerable<ColorSwatch> palette, ExportFormat format)
        {
            return format switch
            {
                ExportFormat.Json => ExportJson(palette),
                ExportFormat.Css => ExportCss(palette),
                ExportFormat.Scss => ExportScss(palette),
                ExportFormat.Less => ExportLess(palette),
                ExportFormat.Tailwind => ExportTailwind(palette),
                ExportFormat.Svg => ExportSvg(palette),
                ExportFormat.Gpl => ExportGpl(palette),
                ExportFormat.Ase or ExportFormat.Aco =>
                    $"{format} is a binary swatch format — use “Save to file…” to write it.",
                _ => ExportPlainText(palette),
            };
        }

        /// <summary>File extension for a format (used by Save-to-file / presets).</summary>
        public static string ExtensionFor(ExportFormat format) => format switch
        {
            ExportFormat.Json => "json",
            ExportFormat.Css => "css",
            ExportFormat.Scss => "scss",
            ExportFormat.Less => "less",
            ExportFormat.Tailwind => "js",
            ExportFormat.Svg => "svg",
            ExportFormat.Gpl => "gpl",
            ExportFormat.Ase => "ase",
            ExportFormat.Aco => "aco",
            _ => "txt",
        };

        private static string ExportLess(IEnumerable<ColorSwatch> palette)
        {
            var sb = new StringBuilder();
            var i = 1;
            foreach (var swatch in palette)
            {
                sb.AppendLine($"@{Slugify(swatch.Name, i)}: {ColorValue(swatch)};");
                i++;
            }
            return sb.ToString();
        }

        /// <summary>Vector swatch sheet — every swatch as a labelled color band (§6).</summary>
        private static string ExportSvg(IEnumerable<ColorSwatch> palette)
        {
            var swatches = palette.ToList();
            if (swatches.Count == 0) return string.Empty;

            const int cellWidth = 180;
            const int height = 260;
            var width = cellWidth * swatches.Count;

            var sb = new StringBuilder();
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
            sb.AppendLine("  <style>text{font-family:sans-serif;} .hex{font-size:13px;} .name{font-size:14px;font-weight:600;}</style>");

            for (var i = 0; i < swatches.Count; i++)
            {
                var swatch = swatches[i];
                var x = i * cellWidth;
                var fill = ColorValue(swatch);
                var text = ContrastText(swatch.Color);

                sb.AppendLine($"  <rect x=\"{x}\" y=\"0\" width=\"{cellWidth}\" height=\"{height}\" fill=\"{fill}\"/>");
                sb.AppendLine($"  <rect x=\"{x + 12}\" y=\"12\" width=\"{cellWidth - 24}\" height=\"{height - 24}\" fill=\"none\" stroke=\"{text}\" stroke-opacity=\"0.35\" rx=\"10\"/>");
                sb.AppendLine($"  <text x=\"{x + 24}\" y=\"{height - 46}\" fill=\"{text}\" class=\"name\">{EscapeXml(swatch.Name)}</text>");
                sb.AppendLine($"  <text x=\"{x + 24}\" y=\"{height - 26}\" fill=\"{text}\" class=\"hex\">{ColorMathService.ToHex(swatch.Color)}</text>");
            }

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        /// <summary>GIMP palette (.gpl) — the de-facto plain-text interchange format (§5/§6).</summary>
        public static string ExportGpl(IEnumerable<ColorSwatch> palette)
        {
            var sb = new StringBuilder();
            sb.AppendLine("GIMP Palette");
            sb.AppendLine("Name: Spectrum");
            sb.AppendLine("Columns: 5");
            sb.AppendLine("#");

            foreach (var swatch in palette)
            {
                sb.AppendLine($"{swatch.Color.R,3} {swatch.Color.G,3} {swatch.Color.B,3}\t{swatch.Name}");
            }

            return sb.ToString();
        }

        private static string EscapeXml(string text) =>
            text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;").Replace("'", "&apos;");

        private static string ContrastText(Color color) =>
            (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0 > 0.55 ? "#101014" : "#FFFFFF";

        private static string ExportJson(IEnumerable<ColorSwatch> palette)
        {
            var data = palette.Select(p => new
            {
                name = p.Name,
                hex = p.Hex,
                alpha = Math.Round(p.Color.A / 255.0, 2)
            });
            return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        }

        private static string ExportCss(IEnumerable<ColorSwatch> palette)
        {
            var sb = new StringBuilder();
            sb.AppendLine(":root {");

            var i = 1;
            foreach (var swatch in palette)
            {
                sb.AppendLine($"  --{Slugify(swatch.Name, i)}: {ColorValue(swatch)};");
                i++;
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string ExportScss(IEnumerable<ColorSwatch> palette)
        {
            var sb = new StringBuilder();

            var i = 1;
            foreach (var swatch in palette)
            {
                sb.AppendLine($"${Slugify(swatch.Name, i)}: {ColorValue(swatch)};");
                i++;
            }

            return sb.ToString();
        }

        private static string ExportTailwind(IEnumerable<ColorSwatch> palette)
        {
            var sb = new StringBuilder();
            sb.AppendLine("module.exports = {");
            sb.AppendLine("  theme: {");
            sb.AppendLine("    extend: {");
            sb.AppendLine("      colors: {");

            var i = 1;
            foreach (var swatch in palette)
            {
                sb.AppendLine($"        '{Slugify(swatch.Name, i)}': '{ColorValue(swatch)}',");
                i++;
            }

            sb.AppendLine("      },");
            sb.AppendLine("    },");
            sb.AppendLine("  },");
            sb.AppendLine("};");
            return sb.ToString();
        }

        private static string ExportPlainText(IEnumerable<ColorSwatch> palette)
        {
            var sb = new StringBuilder();
            foreach (var swatch in palette)
            {
                var suffix = swatch.Color.A < 255 ? $" (alpha {swatch.Color.A / 255.0:P0})" : string.Empty;
                sb.AppendLine($"{swatch.Name}: {swatch.Hex}{suffix}");
            }

            return sb.ToString();
        }

        // Opaque colors export as plain hex; translucent ones export as
        // rgba(...) since hex-with-alpha isn't universally supported by
        // the CSS/SCSS/Tailwind consumers of these strings.
        private static string ColorValue(ColorSwatch swatch)
        {
            return swatch.Color.A < 255
                ? $"rgba({swatch.Color.R}, {swatch.Color.G}, {swatch.Color.B}, {swatch.Color.A / 255.0:F2})"
                : swatch.Hex;
        }

        private static string Slugify(string name, int fallbackIndex)
        {
            var chars = name.ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray();

            var slug = new string(chars).Trim('-');
            return string.IsNullOrWhiteSpace(slug) ? $"color-{fallbackIndex}" : slug;
        }
    }
}
