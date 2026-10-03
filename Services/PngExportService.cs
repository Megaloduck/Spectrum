using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Spectrum.Models;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Spectrum.Services
{
    /// <summary>
    /// §6 "Export as PNG (swatch sheet)": renders the palette with Avalonia's
    /// own retained-mode drawing into a RenderTargetBitmap and saves a PNG.
    /// Uses the Skia renderer already shipped with Avalonia — no new packages.
    /// </summary>
    public static class PngExportService
    {
        private const int CellWidth = 220;
        private const int SheetHeight = 320;

        public static void Export(IEnumerable<ColorSwatch> palette, string path)
        {
            var swatches = palette.ToList();
            var width = System.Math.Max(CellWidth, CellWidth * swatches.Count);

            using var surface = new RenderTargetBitmap(new PixelSize(width, SheetHeight));
            using (var dc = surface.CreateDrawingContext())
            {
                // Opaque canvas so translucent swatches read predictably.
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, SheetHeight));

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

            using var stream = File.Create(path);
            surface.Save(stream);
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
