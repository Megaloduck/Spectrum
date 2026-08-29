using Spectrum.Models;
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
                _ => ExportPlainText(palette),
            };
        }

        private static string ExportJson(IEnumerable<ColorSwatch> palette)
        {
            var data = palette.Select(p => new { name = p.Name, hex = p.Hex });
            return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        }

        private static string ExportCss(IEnumerable<ColorSwatch> palette)
        {
            var sb = new StringBuilder();
            sb.AppendLine(":root {");

            var i = 1;
            foreach (var swatch in palette)
            {
                sb.AppendLine($"  --{Slugify(swatch.Name, i)}: {swatch.Hex};");
                i++;
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string ExportPlainText(IEnumerable<ColorSwatch> palette)
        {
            var sb = new StringBuilder();
            foreach (var swatch in palette)
            {
                sb.AppendLine($"{swatch.Name}: {swatch.Hex}");
            }

            return sb.ToString();
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
