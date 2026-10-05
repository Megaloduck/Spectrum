using Avalonia.Media;
using System;
using System.Runtime.InteropServices;

namespace Spectrum.Services
{
    /// <summary>
    /// System-wide eyedropper primitives (§3): reads raw pixel colors from the
    /// screen using Win32 GDI. Fully local — no network, no capture of content,
    /// just the color under the cursor. Stubbed off on non-Windows platforms.
    /// </summary>
    public static class ScreenPickerService
    {
        public static bool IsSupported => OperatingSystem.IsWindows();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hdc, int x, int y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public static (int X, int Y) GetCursorPos()
        {
            if (!GetCursorPos(out var point)) return (0, 0);
            return (point.X, point.Y);
        }

        public static Color GetColorAt(int x, int y)
        {
            if (!IsSupported) return Colors.Transparent;

            var hdc = GetDC(IntPtr.Zero);
            try
            {
                var packed = GetPixel(hdc, x, y);
                return Color.FromRgb(
                    (byte)(packed & 0xFF),
                    (byte)((packed >> 8) & 0xFF),
                    (byte)((packed >> 16) & 0xFF));
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hdc);
            }
        }

        public static Color GetColorAtCursor()
        {
            var (x, y) = GetCursorPos();
            return GetColorAt(x, y);
        }

        /// <summary>Samples a square patch around a point for the magnifier grid.</summary>
        public static Color[] GetPatch(int centerX, int centerY, int size)
        {
            var result = new Color[size * size];
            var half = size / 2;
            for (var row = 0; row < size; row++)
            {
                for (var col = 0; col < size; col++)
                {
                    result[row * size + col] = GetColorAt(centerX - half + col, centerY - half + row);
                }
            }

            return result;
        }
    }
}
