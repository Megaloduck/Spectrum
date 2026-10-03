using Spectrum.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Spectrum.Services
{
    /// <summary>
    /// §5 "Import (Local Files Only)": parses palette/swatch files into the
    /// library's swatch DTOs. All parsers are pure C# over local bytes — no
    /// network, no external tooling. Supported: own JSON (via the library),
    /// CSS custom properties, GIMP .gpl, Adobe .ase, Photoshop .aco, image
    /// extraction (routed through ImageColorExtractionService by the caller).
    /// </summary>
    public static class PaletteImportService
    {
        /// <summary>Imports a file by extension; returns null when nothing usable was found.</summary>
        public static async System.Threading.Tasks.Task<List<PaletteSwatchDto>?> ImportFileAsync(string path)
        {
            try
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                return ext switch
                {
                    ".css" or ".scss" or ".less" => ParseCssVariables(await File.ReadAllTextAsync(path)),
                    ".gpl" => ParseGpl(await File.ReadAllTextAsync(path)),
                    ".ase" => ParseAse(await File.ReadAllBytesAsync(path)),
                    ".aco" => ParseAco(await File.ReadAllBytesAsync(path)),
                    _ => null,
                };
            }
            catch
            {
                return null;
            }
        }

        // ---------------- CSS custom properties ----------------

        private static readonly Regex CssVariableRegex =
            new(@"--([A-Za-z0-9_-]+)\s*:\s*([^;}]+)", RegexOptions.Compiled);

        public static List<PaletteSwatchDto>? ParseCssVariables(string css)
        {
            var result = new List<PaletteSwatchDto>();
            foreach (Match match in CssVariableRegex.Matches(css))
            {
                var rawName = match.Groups[1].Value;
                var value = match.Groups[2].Value.Trim();

                // Drop !important and functions we can't turn into a color.
                value = value.Replace("!important", "").Trim();
                if (value.StartsWith("var(")) continue;
                if (!ColorFormatService.TryParse(value, out var color)) continue;

                var name = Regex.Replace(rawName, @"[-_]+", " ").Trim();
                result.Add(new PaletteSwatchDto(name, color.A, color.R, color.G, color.B, false));
            }

            return result.Count > 0 ? result : null;
        }

        // ---------------- GIMP .gpl ----------------

        private static readonly Regex GplLineRegex =
            new(@"^\s*(\d{1,3})\s+(\d{1,3})\s+(\d{1,3})\s*(.*)$", RegexOptions.Compiled);

        public static List<PaletteSwatchDto>? ParseGpl(string gpl)
        {
            var result = new List<PaletteSwatchDto>();
            foreach (var line in gpl.Split('\n'))
            {
                if (line.StartsWith("GIMP Palette", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.StartsWith("Name:", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.StartsWith("Columns:", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.TrimStart().StartsWith('#')) continue;

                var match = GplLineRegex.Match(line);
                if (!match.Success) continue;

                var r = byte.Parse(match.Groups[1].Value);
                var g = byte.Parse(match.Groups[2].Value);
                var b = byte.Parse(match.Groups[3].Value);
                var name = match.Groups[4].Value.Trim().Trim('\t', '\r');
                if (string.IsNullOrWhiteSpace(name)) name = $"#{r:X2}{g:X2}{b:X2}";

                result.Add(new PaletteSwatchDto(name, 255, r, g, b, false));
            }

            return result.Count > 0 ? result : null;
        }

        // ---------------- Adobe Swatch Exchange (.ase) ----------------

        public static List<PaletteSwatchDto>? ParseAse(byte[] data)
        {
            // 'ASEF' + version(2×u16) + blockCount(u32), all big-endian.
            if (data.Length < 12 ||
                data[0] != (byte)'A' || data[1] != (byte)'S' || data[2] != (byte)'E' || data[3] != (byte)'F')
            {
                return null;
            }

            var result = new List<PaletteSwatchDto>();

            // Header: 'ASEF'(0-3), version major(4-5), minor(6-7), blockCount(8-11) — big-endian.
            var offset = 8;
            var blockCount = ReadUInt32Be(data, ref offset); // offset now points at the first block

            for (var block = 0; block < blockCount && offset + 6 <= data.Length; block++)
            {
                var blockLength = ReadUInt16Be(data, ref offset);
                var blockStart = offset;
                var blockEnd = Math.Min(blockStart + blockLength, data.Length);

                var type = ReadUInt16Be(data, ref offset);
                if (type != 0x0001) // 0xC000/0xC002 are group markers
                {
                    offset = blockEnd;
                    continue;
                }

                // Color name: UTF-16BE, length-prefixed including the null.
                var nameLength = ReadUInt16Be(data, ref offset);
                var nameChars = new char[Math.Max(0, nameLength - 1)];
                for (var i = 0; i < nameChars.Length && offset + 1 < data.Length; i++)
                {
                    nameChars[i] = (char)ReadUInt16Be(data, ref offset);
                }
                if (nameLength > 0 && offset + 1 < data.Length) offset += 2; // null terminator

                // Model: 'RGB ' → 3 × float32 BE (0..1).
                if (offset + 4 > blockEnd) { offset = blockEnd; continue; }
                var model = Encoding.ASCII.GetString(data, offset, 4);
                offset += 4;

                if (model == "RGB " && offset + 12 <= blockEnd)
                {
                    var r = FloatBe(data, ref offset);
                    var g = FloatBe(data, ref offset);
                    var b = FloatBe(data, ref offset);

                    var color = Avalonia.Media.Color.FromRgb(ToByte(r), ToByte(g), ToByte(b));

                    var name = new string(nameChars).Trim('\0');
                    if (string.IsNullOrWhiteSpace(name)) name = ColorMathService.ToHex(color);

                    result.Add(new PaletteSwatchDto(name, 255, color.R, color.G, color.B, false));
                }

                offset = blockEnd;
            }

            return result.Count > 0 ? result : null;
        }

        // ---------------- Photoshop .aco ----------------

        public static List<PaletteSwatchDto>? ParseAco(byte[] data)
        {
            if (data.Length < 4) return null;

            var offset = 0;
            var version = ReadUInt16Be(data, ref offset);
            if (version != 1 && version != 2) return null;

            var count = ReadUInt16Be(data, ref offset);
            var named = new List<PaletteSwatchDto>();

            // v1 records: id, R, G, B (0..65535), color space.
            var colors = new List<(byte R, byte G, byte B, string Name)>();
            for (var i = 0; i < count && offset + 10 <= data.Length; i++)
            {
                offset += 2; // color id
                var w1 = ReadUInt16Be(data, ref offset);
                var w2 = ReadUInt16Be(data, ref offset);
                var w3 = ReadUInt16Be(data, ref offset);
                var space = ReadUInt16Be(data, ref offset);

                switch (space)
                {
                    case 0: // RGB
                        colors.Add((Scale(w1), Scale(w2), Scale(w3), string.Empty));
                        break;
                    case 8: // grayscale — first word carries the value
                        var gray = Scale(w1);
                        colors.Add((gray, gray, gray, string.Empty));
                        break;
                    case 7: // Lab: L unsigned 0..65535 → 0..100; a/b stored signed around 0
                        var lab = new LabColor(
                            w1 / 655.35,
                            unchecked((short)w2) / 256.0,
                            unchecked((short)w3) / 256.0);
                        var labColor = ColorMathService.LabToRgb(lab);
                        colors.Add((labColor.R, labColor.G, labColor.B, string.Empty));
                        break;
                    // Other color spaces (HSB/CMYK) are skipped rather than mis-read.
                }
            }

            // Optional v2 section appends names.
            if (offset + 4 <= data.Length && ReadUInt16Be(data, ref offset) == 2)
            {
                var namedCount = ReadUInt16Be(data, ref offset);
                for (var i = 0; i < namedCount && offset + 2 <= data.Length; i++)
                {
                    if (offset + 10 > data.Length) break;
                    offset += 10; // same record body (ignored — colors came from v1)

                    var nameLength = ReadUInt16Be(data, ref offset); // in UTF-16 code units, incl. null
                    var chars = new char[Math.Max(0, nameLength - 1)];
                    for (var c = 0; c < chars.Length && offset + 1 < data.Length; c++)
                        chars[c] = (char)ReadUInt16Be(data, ref offset);
                    if (nameLength > 0 && offset + 1 < data.Length) offset += 2;

                    var name = new string(chars).Trim('\0');
                    if (i < colors.Count && !string.IsNullOrWhiteSpace(name))
                    {
                        var (r, g, b, _) = colors[i];
                        named.Add(new PaletteSwatchDto(name, 255, r, g, b, false));
                    }
                }
            }

            if (named.Count > 0) return named;

            return colors.Count > 0
                ? colors.Select((c, i) =>
                {
                    var hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                    return new PaletteSwatchDto(string.IsNullOrWhiteSpace(c.Name) ? hex : c.Name, 255, c.R, c.G, c.B, false);
                }).ToList()
                : null;
        }

        // ---------------- Helpers ----------------

        private static byte Scale(ushort value) => (byte)Math.Clamp(value * 255 / 65535, 0, 255);

        private static byte ToByte(float unit) => (byte)Math.Clamp(Math.Round(unit * 255), 0, 255);

        private static ushort ReadUInt16Be(byte[] data, ref int offset)
        {
            var value = (ushort)((data[offset] << 8) | data[offset + 1]);
            offset += 2;
            return value;
        }

        private static uint ReadUInt32Be(byte[] data, ref int offset)
        {
            var value = ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) |
                         ((uint)data[offset + 2] << 8) | data[offset + 3];
            offset += 4;
            return value;
        }

        private static float FloatBe(byte[] data, ref int offset)
        {
            var bytes = new[] { data[offset + 3], data[offset + 2], data[offset + 1], data[offset] };
            offset += 4;
            var buffer = BitConverter.ToSingle(bytes, 0);
            if (float.IsNaN(buffer) || float.IsInfinity(buffer)) return 0;
            return Math.Clamp(buffer, 0f, 1f);
        }
    }
}
