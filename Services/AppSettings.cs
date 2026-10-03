using System;
using System.Collections.Generic;

namespace Spectrum.Services
{
    /// <summary>Persisted app settings (§10) — one JSON document next to the library.</summary>
    public class AppSettings
    {
        // Appearance
        public string Theme { get; set; } = "System";              // System | Light | Dark
        public string? AccentHex { get; set; }                      // null = default accent
        public string Density { get; set; } = "Comfortable";        // Comfortable | Compact

        // Copy / export defaults (§10 "Default color format on copy", "Default export format")
        public string DefaultCopyFormat { get; set; } = "Hex";
        public string DefaultExportFormat { get; set; } = "Json";

        // Hotkey for the screen eyedropper (§3 global hotkey, §10 hotkey configuration)
        public string HotkeyKey { get; set; } = "P";
        public bool HotkeyCtrl { get; set; } = true;
        public bool HotkeyAlt { get; set; } = true;
        public bool HotkeyShift { get; set; }
        public bool HotkeyMeta { get; set; }

        // Storage (§9/§10 "Storage location setting")
        public string StorageProvider { get; set; } = "json";       // json | sqlite (§10)
        public string? StorageDirectory { get; set; }               // null = %APPDATA%/Spectrum

        // Recently picked colors (§3 history) — hex strings, newest first.
        public List<string> RecentPicks { get; set; } = new();
    }
}
