using Avalonia.Threading;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spectrum.Services
{
    /// <summary>
    /// Registers a system-wide hotkey (§3 "Global hotkey to trigger picker",
    /// §10 "Hotkey configuration"). A dedicated background thread owns the
    /// hotkey: RegisterHotKey with a null HWND posts WM_HOTKEY straight to the
    /// registering thread's message queue, so the thread only needs a GetMessage
    /// loop — no window creation, no subclassing, no interference with Avalonia's
    /// own message pump. HotkeyRaised is marshalled onto the UI thread.
    ///
    /// Windows-only; on macOS/Linux Register() is a no-op and IsSupported=false
    /// so the UI can explain the limitation instead of failing silently.
    /// </summary>
    public static class GlobalHotkeyService
    {
        public static bool IsSupported => OperatingSystem.IsWindows();

        /// <summary>True once the message-loop thread exists (bind changes then go through UpdateBinding).</summary>
        public static bool IsRegistered => _thread is not null;

        /// <summary>Raised on the UI thread whenever the global hotkey fires.</summary>
        public static event Action? HotkeyRaised;

        private const int WmHotkey = 0x0312;
        private const int WmAppUpdate = 0x8000 + 42; // WM_APP + 42: rebind request
        private const int HotkeyId = 0x5350;         // arbitrary unique id

        private static Thread? _thread;
        private static int _threadId;
        private static uint _modifiers;
        private static uint _virtualKey;

        // Published before the thread starts; only the hotkey thread reads them
        // after that (volatile for the rebind handshake).
        private static volatile uint _pendingModifiers;
        private static volatile uint _pendingVirtualKey;

        public static void Register(uint modifiers, uint virtualKey)
        {
            if (!IsSupported || _thread is not null) return;

            _modifiers = _pendingModifiers = modifiers;
            _virtualKey = _pendingVirtualKey = virtualKey;

            _thread = new Thread(MessageLoop)
            {
                IsBackground = true,
                Name = "SpectrumGlobalHotkey",
            };
            if (OperatingSystem.IsWindows())
                _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        /// <summary>Re-registers with a new key combination (Settings → hotkeys).</summary>
        public static void UpdateBinding(uint modifiers, uint virtualKey)
        {
            if (!IsSupported || _thread is null) return;

            _pendingModifiers = modifiers;
            _pendingVirtualKey = virtualKey;
            PostThreadMessage(_threadId, WmAppUpdate, IntPtr.Zero, IntPtr.Zero);
        }

        private static void MessageLoop()
        {
            _threadId = GetCurrentThreadId();

            RegisterHotKey(IntPtr.Zero, HotkeyId, _modifiers, _virtualKey);

            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.Message == WmHotkey && msg.WParam.ToInt32() == HotkeyId)
                {
                    Dispatcher.UIThread.Post(() => HotkeyRaised?.Invoke());
                }
                else if (msg.Message == WmAppUpdate)
                {
                    UnregisterHotKey(IntPtr.Zero, HotkeyId);
                    RegisterHotKey(IntPtr.Zero, HotkeyId, _pendingModifiers, _pendingVirtualKey);
                }

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        /// <summary>Maps an Avalonia key + modifiers to a Windows virtual key + RegisterHotKey modifiers.</summary>
        public static (uint Modifiers, uint VirtualKey) ToWindowsBinding(Avalonia.Input.Key key, bool ctrl, bool alt, bool shift, bool meta)
        {
            const uint modAlt = 0x0001, modCtrl = 0x0002, modShift = 0x0004, modWin = 0x0008;

            uint modifiers = 0;
            if (ctrl) modifiers |= modCtrl;
            if (alt) modifiers |= modAlt;
            if (shift) modifiers |= modShift;
            if (meta) modifiers |= modWin;

            return (modifiers, KeyToVirtualKey(key));
        }

        /// <summary>Covers the keys the settings screen realistically offers.</summary>
        public static uint KeyToVirtualKey(Avalonia.Input.Key key)
        {
            var name = key.ToString();
            if (name.Length == 1)
            {
                var ch = name[0];
                if (ch is >= 'A' and <= 'Z') return ch;
                if (ch is >= '0' and <= '9') return ch;
            }

            if (name.StartsWith("F") && int.TryParse(name.Substring(1), out var f) && f is >= 1 and <= 24)
                return (uint)(0x70 + f - 1); // VK_F1..VK_F24

            return key switch
            {
                Avalonia.Input.Key.Space => 0x20,
                Avalonia.Input.Key.Tab => 0x09,
                Avalonia.Input.Key.Enter => 0x0D,
                Avalonia.Input.Key.Escape => 0x1B,
                Avalonia.Input.Key.OemComma => 0xBC,
                Avalonia.Input.Key.OemPeriod => 0xBE,
                Avalonia.Input.Key.OemMinus => 0xBD,
                Avalonia.Input.Key.OemPlus => 0xBB,
                _ => 0,
            };
        }

        // ---------------- Win32 plumbing ----------------

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr Hwnd;
            public uint Message;
            public IntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public POINT Point;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG msg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG msg);

        [DllImport("user32.dll")]
        private static extern bool PostThreadMessage(int threadId, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern int GetCurrentThreadId();
    }
}
