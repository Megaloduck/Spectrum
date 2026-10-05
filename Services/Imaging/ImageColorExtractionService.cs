using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Runtime.InteropServices;

namespace Spectrum.Services
{
    /// <summary>
    /// Picks a handful of representative colors out of a bitmap. This is a
    /// deliberately simple histogram approach (not k-means or anything
    /// fancier): downscale so the pass is fast, bucket every pixel into a
    /// coarse color bin, then return the average color of the most
    /// frequently hit bins.
    /// </summary>
    public static class ImageColorExtractionService
    {
        private const int SampleDimension = 64; // downscale target, in pixels
        private const int BucketBits = 5;        // 2^5 = 32 levels per channel

        public static List<Color> ExtractDominantColors(Bitmap source, int count)
        {
            using var scaled = source.CreateScaledBitmap(new PixelSize(SampleDimension, SampleDimension));

            var width = scaled.PixelSize.Width;
            var height = scaled.PixelSize.Height;
            var stride = width * 4; // assumes a 4-byte-per-pixel (Bgra8888) decode, the common case
            var pixels = new byte[stride * height];

            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                scaled.CopyPixels(new PixelRect(0, 0, width, height), handle.AddrOfPinnedObject(), pixels.Length, stride);
            }
            finally
            {
                handle.Free();
            }

            var buckets = new Dictionary<int, (long R, long G, long B, int Count)>();

            for (var i = 0; i + 3 < pixels.Length; i += 4)
            {
                byte b = pixels[i];
                byte g = pixels[i + 1];
                byte r = pixels[i + 2];
                byte a = pixels[i + 3];

                if (a < 16) continue; // skip near-transparent pixels

                var key = ((r >> BucketBits) << 10) | ((g >> BucketBits) << 5) | (b >> BucketBits);

                if (buckets.TryGetValue(key, out var existing))
                {
                    buckets[key] = (existing.R + r, existing.G + g, existing.B + b, existing.Count + 1);
                }
                else
                {
                    buckets[key] = (r, g, b, 1);
                }
            }

            return buckets
                .OrderByDescending(kv => kv.Value.Count)
                .Take(count)
                .Select(kv => Color.FromRgb(
                    (byte)(kv.Value.R / kv.Value.Count),
                    (byte)(kv.Value.G / kv.Value.Count),
                    (byte)(kv.Value.B / kv.Value.Count)))
                .ToList();
        }
    }
}
