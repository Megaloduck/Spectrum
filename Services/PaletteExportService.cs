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
        Tailwind,
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
                ExportFormat.Tailwind => ExportTailwind(palette),
                _ => ExportPlainText(palette),
            };
        }

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
