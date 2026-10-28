using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Spectrum.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Spectrum.Services
{
    /// <summary>Raster formats the swatch sheet can be written as.</summary>
    public enum SheetImageFormat
    {
        Png,
        Jpg,
    }

    /// <summary>
    /// §6 "Export as PNG (swatch sheet)": renders the palette with Avalonia's
    /// own retained-mode drawing into a RenderTargetBitmap — the same Skia
    /// renderer already shipped with Avalonia, no new packages.
    ///
    /// The render is split from the write so the Export workspace can show the
    /// exact image it is about to save (and re-use it for the file).
    /// </summary>
    public static class SwatchSheetService
    {
        private const int CellWidth = 220;
        private const int SheetHeight = 320;

        // Avalonia's Bitmap.Save only encodes PNG (11.x), so JPEG goes through
        // Skia directly — the same engine Avalonia.Skia renders this sheet with.
        private const int JpegQuality = 90;

        /// <summary>Pixel size of the sheet for a palette of this length.</summary>
        public static PixelSize SizeFor(int swatchCount) =>
            new(System.Math.Max(CellWidth, CellWidth * swatchCount), SheetHeight);

        /// <summary>File extension for a sheet image format.</summary>
        public static string ExtensionFor(SheetImageFormat format) =>
            format == SheetImageFormat.Jpg ? "jpg" : "png";

        /// <summary>Label used in buttons and file pickers.</summary>
        public static string DisplayName(SheetImageFormat format) =>
            format == SheetImageFormat.Jpg ? "JPG" : "PNG";

        /// <summary>Draws the sheet (one labelled cell per swatch) without touching the disk.</summary>
        public static RenderTargetBitmap Render(IEnumerable<ColorSwatch> palette)
        {
            var swatches = palette.ToList();
            var size = SizeFor(swatches.Count);

            var surface = new RenderTargetBitmap(size);
            using (var dc = surface.CreateDrawingContext())
            {
                // Opaque canvas so translucent swatches read predictably.
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, size.Width, size.Height));

                for (var i = 0; i < swatches.Count; i++)
                {
                    var swatch = swatches[i];
                    var x = i * CellWidth;
                    var blockRect = new Rect(x + 10, 10, CellWidth - 20, SheetHeight - 90);

                    // Checkerboard under the color so alpha is visible.
                    DrawChecker(dc, blockRect);
                    dc.DrawRectangle(new SolidColorBrush(swatch.Color), null, blockRect);

                    var textColor = PickReadable(swatch.Color);
                    var borderPen = new Pen(new SolidColorBrush(textColor) { Opacity = 0.4 }, 1);
                    dc.DrawRectangle(null, borderPen, blockRect);

                    var name = string.IsNullOrWhiteSpace(swatch.Name) ? "Color" : swatch.Name;
                    DrawText(dc, name, x + 14, SheetHeight - 66, 16, FontWeight.SemiBold, textColor);
                    DrawText(dc, ColorMathService.ToHex(swatch.Color), x + 14, SheetHeight - 42, 14,
                        FontWeight.Normal, textColor);
                    DrawText(dc, $"rgb({swatch.Color.R}, {swatch.Color.G}, {swatch.Color.B})", x + 14,
                        SheetHeight - 22, 12, FontWeight.Normal, textColor);
                }
            }

            return surface;
        }

        /// <summary>Writes a sheet produced by <see cref="Render"/> to disk.</summary>
        public static void Save(RenderTargetBitmap sheet, string path, SheetImageFormat format)
        {
            if (format == SheetImageFormat.Jpg)
            {
                SaveJpeg(sheet, path);
                return;
            }

            using var stream = File.Create(path);
            sheet.Save(stream);
        }

        private static void SaveJpeg(RenderTargetBitmap sheet, string path)
        {
            using var png = new MemoryStream();
            sheet.Save(png);
            png.Position = 0;

            using var bitmap = SKBitmap.Decode(png)
                ?? throw new InvalidOperationException("The swatch sheet could not be read back for JPEG encoding.");
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality)
                ?? throw new InvalidOperationException("The swatch sheet could not be encoded as JPEG.");

            using var file = File.Create(path);
            data.SaveTo(file);
        }

        private static void DrawChecker(Avalonia.Media.DrawingContext dc, Rect rect)
        {
            const int cell = 10;
            var light = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
            var dark = new SolidColorBrush(Color.FromRgb(0xCF, 0xCF, 0xCF));

            for (var y = 0; y < rect.Height; y += cell)
            {
                for (var x = 0; x < rect.Width; x += cell)
                {
                    var alternate = ((x / cell) + (y / cell)) % 2 == 0;
                    dc.DrawRectangle(alternate ? light : dark, null,
                        new Rect(rect.X + x, rect.Y + y,
                            System.Math.Min(cell, rect.Width - x),
                            System.Math.Min(cell, rect.Height - y)));
                }
            }
        }

        private static void DrawText(Avalonia.Media.DrawingContext dc, string text, double x, double y,
            double size, FontWeight weight, Color color)
        {
            var formatted = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, weight),
                size,
                new SolidColorBrush(color));

            dc.DrawText(formatted, new Point(x, y));
        }

        private static Color PickReadable(Color color) =>
            (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0 > 0.55
                ? Color.FromRgb(0x1E, 0x20, 0x25)
                : Colors.White;
    }
}
