using Avalonia.Media;
using Spectrum.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Spectrum.Services
{
    /// <summary>
    /// §6 "Export as ASE / ACO": writers for the two binary Adobe swatch
    /// formats (GPL is plain text — see PaletteExportService.ExportGpl).
    /// Pure byte-level writers, fully offline.
    /// </summary>
    public static class PaletteBinaryExportService
    {
        // ---------------- Adobe Swatch Exchange ----------------

        public static byte[] WriteAse(IEnumerable<ColorSwatch> palette)
        {
            var swatches = new List<ColorSwatch>(palette);

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);

            // Header: 'ASEF', version 1.0, block count — all big-endian.
            w.Write((byte)'A');
            w.Write((byte)'S');
            w.Write((byte)'E');
            w.Write((byte)'F');
            WriteUInt16Be(w, 1);
            WriteUInt16Be(w, 0);
            WriteUInt32Be(w, (uint)swatches.Count);

            foreach (var swatch in swatches)
            {
                var name = string.IsNullOrWhiteSpace(swatch.Name) ? ColorMathService.ToHex(swatch.Color) : swatch.Name;
                var nameUtf16 = Encoding.Unicode.GetBytes(name + "\0"); // UTF-16BE == byte-swapped UTF-16
                var nameLength = (ushort)(name.Length + 1);              // in UTF-16 code units, incl. null

                // type(2) + nameLength(2) + name + model(4) + 3×float32(12)
                var blockLength = (ushort)(2 + 2 + nameUtf16.Length + 4 + 12);

                WriteUInt16Be(w, blockLength);
                WriteUInt16Be(w, 0x0001);      // color entry
                WriteUInt16Be(w, nameLength);
                WriteUtf16Be(w, name + "\0");
                w.Write(Encoding.ASCII.GetBytes("RGB "));
                WriteFloatBe(w, swatch.Color.R / 255f);
                WriteFloatBe(w, swatch.Color.G / 255f);
                WriteFloatBe(w, swatch.Color.B / 255f);
            }

            w.Flush();
            return ms.ToArray();
        }

        // ---------------- Photoshop Color Swatches ----------------

        public static byte[] WriteAco(IEnumerable<ColorSwatch> palette)
        {
            var swatches = new List<ColorSwatch>(palette);

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);

            // Version 1 section: color records without names.
            WriteUInt16Be(w, 1);
            WriteUInt16Be(w, (ushort)swatches.Count);
            var id = 1;
            foreach (var swatch in swatches)
            {
                WriteAcoRecord(w, swatch.Color, id++);
            }

            // Version 2 section: same records + UTF-16 names.
            WriteUInt16Be(w, 2);
            WriteUInt16Be(w, (ushort)swatches.Count);
            id = 1;
            foreach (var swatch in swatches)
            {
                WriteAcoRecord(w, swatch.Color, id++);
                var name = string.IsNullOrWhiteSpace(swatch.Name) ? ColorMathService.ToHex(swatch.Color) : swatch.Name;
                WriteUInt16Be(w, (ushort)(name.Length + 1)); // includes null
                WriteUtf16Be(w, name + "\0");
            }

            w.Flush();
            return ms.ToArray();
        }

        private static void WriteAcoRecord(BinaryWriter w, Color color, int id)
        {
            WriteUInt16Be(w, (ushort)id);
            WriteUInt16Be(w, ScaleTo65535(color.R));
            WriteUInt16Be(w, ScaleTo65535(color.G));
            WriteUInt16Be(w, ScaleTo65535(color.B));
            WriteUInt16Be(w, 0); // color space: RGB
        }

        private static ushort ScaleTo65535(byte value) => (ushort)(value * 257);

        // ---------------- Big-endian primitives ----------------

        private static void WriteUInt16Be(BinaryWriter w, ushort value)
        {
            w.Write((byte)(value >> 8));
            w.Write((byte)(value & 0xFF));
        }

        private static void WriteUInt32Be(BinaryWriter w, uint value)
        {
            w.Write((byte)(value >> 24));
            w.Write((byte)((value >> 16) & 0xFF));
            w.Write((byte)((value >> 8) & 0xFF));
            w.Write((byte)(value & 0xFF));
        }

        private static void WriteFloatBe(BinaryWriter w, float value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            w.Write(bytes);
        }

        /// <summary>UTF-16BE text (each code unit byte-swapped on little-endian machines).</summary>
        private static void WriteUtf16Be(BinaryWriter w, string text)
        {
            foreach (var ch in text)
            {
                w.Write((byte)(ch >> 8));
                w.Write((byte)(ch & 0xFF));
            }
        }
    }
}
