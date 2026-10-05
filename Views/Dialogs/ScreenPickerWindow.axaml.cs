using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Spectrum.Services;
using System;
using System.Runtime.InteropServices;

namespace Spectrum.Views
{
    /// <summary>
    /// System-wide eyedropper overlay (§3): a small topmost window that follows
    /// the cursor showing a live 12×12 magnifier sampled straight from the
    /// screen, plus a WH_MOUSE_LL hook so a click *anywhere* (even over other
    /// applications) captures the color under the cursor. Esc cancels.
    /// Fully local — GDI GetPixel only, no network.
    /// </summary>
    public partial class ScreenPickerWindow : Window
    {
        private const int MagnifierSize = 12;
        private const int WhMouseLl = 14;
        private const int WmLeftButtonDown = 0x0201;

        private readonly Border[] _cells = new Border[MagnifierSize * MagnifierSize];
        private readonly DispatcherTimer _timer;
        private readonly LowLevelMouseProc _hookProc;
        private IntPtr _hookHandle;
        private bool _resolved;

        public ScreenPickerWindow()
        {
            InitializeComponent();

            for (var i = 0; i < _cells.Length; i++)
            {
                _cells[i] = new Border
                {
                    Width = 14,
                    Height = 14,
                    Background = Brushes.Transparent,
                };
                Magnifier.Children.Add(_cells[i]);
            }

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _timer.Tick += (_, _) => Update();

            _hookProc = HookCallback;
            Opened += (_, _) =>
            {
                _timer.Start();
                Update();
                if (ScreenPickerService.IsSupported)
                    _hookHandle = SetWindowsHookEx(WhMouseLl, _hookProc, GetModuleHandle(null), 0);
                Focus();
            };
            Closed += (_, _) =>
            {
                _timer.Stop();
                if (_hookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookHandle);
                    _hookHandle = IntPtr.Zero;
                }
            };
        }

        private void Update()
        {
            var (x, y) = ScreenPickerService.GetCursorPos();

            // Follow the cursor, clamped to the screen working area.
            try
            {
                var screen = Screens.ScreenFromPoint(new PixelPoint(x, y)) ?? Screens.Primary;
                if (screen is not null)
                {
                    var area = screen.WorkingArea;
                    var left = Math.Min(x + 24, area.Right - Bounds.Width - 4);
                    var top = Math.Min(y + 24, area.Bottom - Bounds.Height - 4);
                    Position = new PixelPoint(Math.Max((int)left, area.X + 4), Math.Max((int)top, area.Y + 4));
                }
            }
            catch
            {
                Position = new PixelPoint(x + 24, y + 24);
            }

            // Live magnifier + readout.
            var patch = ScreenPickerService.GetPatch(x, y, MagnifierSize);
            var center = MagnifierSize / 2 * MagnifierSize + MagnifierSize / 2;
            for (var i = 0; i < _cells.Length; i++)
            {
                _cells[i].Background = new SolidColorBrush(patch[i]);
            }

            var color = patch[center];
            Swatch.Background = new SolidColorBrush(color);
            var hex = ColorMathService.ToHex(color);
            HexLabel.Text = hex;
            HexLabel.Foreground = GetReadable(color);
        }

        private static IBrush GetReadable(Color color) =>
            (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0 > 0.6
                ? new SolidColorBrush(Color.FromRgb(0x1E, 0x20, 0x25))
                : Brushes.White;

        private void Pick()
        {
            if (_resolved) return;
            _resolved = true;
            var color = ScreenPickerService.GetColorAtCursor();
            Close(color);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            if (_resolved) return;
            _resolved = true;
            Close(null);
        }

        // Global click capture: the cursor is usually over *another* window,
        // so clicks never reach this overlay without a low-level mouse hook.
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WmLeftButtonDown)
            {
                Dispatcher.UIThread.Post(Pick);
            }

            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);
    }
}
