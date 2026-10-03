using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.IO;
using System.Text.Json;

namespace Spectrum.Services
{
    /// <summary>
    /// §10 Settings: loads/saves settings.json and applies every setting to the
    /// running app (theme, accent, density, defaults, hotkey, storage location).
    /// Everything is local — settings never leave the machine.
    /// </summary>
    public static class SettingsService
    {
        public static AppSettings Settings { get; private set; } = new();

        private static readonly string AppDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spectrum");

        private static string SettingsPath => Path.Combine(AppDirectory, "settings.json");

        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), SerializerOptions)
                               ?? new AppSettings();
                }
            }
            catch
            {
                // Corrupt settings should never stop the app from starting.
                Settings = new AppSettings();
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(AppDirectory);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Settings, SerializerOptions));
            }
            catch
            {
                // Non-fatal: settings simply won't persist this run.
            }
        }

        // ---------------- Apply (each is safe to call repeatedly) ----------------

        /// <summary>Called first, before the library initializes, so the store points at the right folder.</summary>
        public static void ApplyStorageLocation()
        {
            if (PaletteLibraryService.Store is JsonLibraryStore json)
            {
                json.DirectoryPath = string.IsNullOrWhiteSpace(Settings.StorageDirectory)
                    ? AppDirectory
                    : Settings.StorageDirectory;
            }
        }

        public static void ApplyTheme()
        {
            if (Application.Current is null) return;

            Application.Current.RequestedThemeVariant = Settings.Theme switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }

        public static void ApplyAccent()
        {
            if (Application.Current is null) return;

            if (string.IsNullOrWhiteSpace(Settings.AccentHex) ||
                !ColorMathService.TryParseHex(Settings.AccentHex, out var accent))
            {
                accent = Color.FromRgb(0x3B, 0x82, 0xF6);
            }

            var brush = new SolidColorBrush(accent);

            // Prefer overriding the resource inside the theme stylesheet (so every
            // DynamicResource reference re-evaluates); fall back to app resources.
            foreach (var style in Application.Current.Styles)
            {
                if (style is Styles styles && styles.Resources is { } resources &&
                    resources.ContainsKey("AccentBrush"))
                {
                    resources["AccentBrush"] = brush;
                    return;
                }
            }

            Application.Current.Resources["AccentBrush"] = brush;
        }

        public static void ApplyDensity()
        {
            // The window adds/removes the "compact" class (see MainWindow).
            if (Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (var window in desktop.Windows)
                {
                    var compact = Settings.Density == "Compact";
                    if (compact && !window.Classes.Contains("compact")) window.Classes.Add("compact");
                    if (!compact && window.Classes.Contains("compact")) window.Classes.Remove("compact");
                }
            }
        }

        public static void ApplyHotkey()
        {
            if (Enum.TryParse<Avalonia.Input.Key>(Settings.HotkeyKey, out var key))
            {
                var (modifiers, virtualKey) = GlobalHotkeyService.ToWindowsBinding(
                    key, Settings.HotkeyCtrl, Settings.HotkeyAlt, Settings.HotkeyShift, Settings.HotkeyMeta);

                if (virtualKey == 0) return;

                if (!GlobalHotkeyService.IsRegistered)
                    GlobalHotkeyService.Register(modifiers, virtualKey);
                else
                    GlobalHotkeyService.UpdateBinding(modifiers, virtualKey);
            }
        }

        /// <summary>Applies everything that must run before the first window exists.</summary>
        public static void ApplyStartup()
        {
            ApplyStorageLocation();
            ApplyTheme();
            ApplyAccent();
            ApplyHotkey();
        }
    }
}
