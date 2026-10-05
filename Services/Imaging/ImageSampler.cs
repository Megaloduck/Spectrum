using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;

namespace Spectrum.Services
{
    /// <summary>
    /// Decodes a bitmap into an in-memory pixel buffer once, so the in-app
    /// eyedropper (§3) can sample any pixel in O(1) on every mouse move.
    /// Handles both RGBA and BGRA byte orders and un-premultiplies alpha.
    /// </summary>
    public sealed class ImageSampler
    {
        private readonly byte[] _pixels;
        private readonly bool _rgbaOrder;

        public int Width { get; }
        public int Height { get; }

        private ImageSampler(byte[] pixels, int width, int height, bool rgbaOrder)
        {
            _pixels = pixels;
            Width = width;
            Height = height;
            _rgbaOrder = rgbaOrder;
        }

        public static ImageSampler? FromBitmap(Bitmap bitmap)
        {
            var width = bitmap.PixelSize.Width;
            var height = bitmap.PixelSize.Height;
            if (width <= 0 || height <= 0) return null;

            var stride = width * 4;
            var buffer = new byte[stride * height];
            var handle = System.Runtime.InteropServices.Marshal.AllocHGlobal(buffer.Length);
            try
            {
                bitmap.CopyPixels(new PixelRect(0, 0, width, height), handle, buffer.Length, stride);
                System.Runtime.InteropServices.Marshal.Copy(handle, buffer, 0, buffer.Length);
            }
            catch
            {
                return null;
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(handle);
            }

            var formatName = bitmap.Format?.ToString() ?? string.Empty;
            var rgbaOrder = formatName.Contains("Rgba", StringComparison.OrdinalIgnoreCase);

            return new ImageSampler(buffer, width, height, rgbaOrder);
        }

        /// <summary>Samples the image at integer coordinates (clamped), un-premultiplied.</summary>
        public Color Sample(int x, int y)
        {
            x = Math.Clamp(x, 0, Width - 1);
            y = Math.Clamp(y, 0, Height - 1);

            var i = (y * Width + x) * 4;
            var b0 = _pixels[i];
            var b1 = _pixels[i + 1];
            var b2 = _pixels[i + 2];
            var b3 = _pixels[i + 3];

            byte r = _rgbaOrder ? b2 : b0;
            byte g = _rgbaOrder ? b1 : b1;
            byte blue = _rgbaOrder ? b0 : b2;
            var a = b3;

            if (a > 0 && a < 255)
            {
                r = (byte)Math.Min(255, r * 255 / a);
                g = (byte)Math.Min(255, g * 255 / a);
                blue = (byte)Math.Min(255, blue * 255 / a);
            }

            return Color.FromArgb(a, r, g, blue);
        }
    }
}
