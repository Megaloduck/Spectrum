using System;
using System.Collections.Generic;

namespace Spectrum.Services
{
    /// <summary>Persisted app settings — one JSON document next to the library.</summary>
    public class AppSettings
    {
        // Appearance
        public string Theme { get; set; } = "System";              // System | Light | Dark
        public string? AccentHex { get; set; }                      // null = default accent
        public string Density { get; set; } = "Comfortable";        // Comfortable | Compact

        // Copy / export defaults ("Default color format on copy", "Default export format")
        public string DefaultCopyFormat { get; set; } = "Hex";
        public string DefaultExportFormat { get; set; } = "Json";

        // Hotkey for the screen eyedropper (global hotkey, hotkey configuration)
        public string HotkeyKey { get; set; } = "P";
        public bool HotkeyCtrl { get; set; } = true;
        public bool HotkeyAlt { get; set; } = true;
        public bool HotkeyShift { get; set; }
        public bool HotkeyMeta { get; set; }

        // Storage ("Storage location setting")
        public string StorageProvider { get; set; } = "json";       // json | sqlite 
        public string? StorageDirectory { get; set; }               // null = %APPDATA%/Spectrum

        // Recently picked colors (history) — hex strings, newest first.
        public List<string> RecentPicks { get; set; } = new();
    }
}
