using CommunityToolkit.Mvvm.ComponentModel;
using Spectrum.Models;
using Spectrum.Services;
using System;
using System.Linq;

namespace Spectrum.ViewModels
{
    /// <summary>
    /// §10 Settings: every option applies immediately and persists to
    /// settings.json. Sections: appearance (theme/accent/density), copy &
    /// export defaults, hotkey, storage location/provider, danger zone.
    /// </summary>
    public partial class SettingsViewModel : ViewModelBase
    {
        public static readonly string[] Themes = { "System", "Light", "Dark" };
        public static readonly string[] Densities = { "Comfortable", "Compact" };
        public static readonly string[] StorageProviders = { "json", "sqlite" };

        public Array CopyFormats { get; } = Enum.GetValues(typeof(CopyFormat));
        public Array ExportFormats { get; } = Enum.GetValues(typeof(ExportFormat));

        /// <summary>Keys offered by the hotkey picker: letters, digits and function keys.</summary>
        public static readonly string[] HotkeyKeys =
            Enumerable.Range('A', 26).Select(c => ((char)c).ToString())
                .Concat(Enumerable.Range('0', 10).Select(c => ((char)c).ToString()))
                .Concat(Enumerable.Range(1, 12).Select(f => "F" + f))
                .Concat(new[] { "Space" })
                .ToArray();

        // ---------------- Appearance ----------------

        [ObservableProperty] private string _theme;
        [ObservableProperty] private string? _accentHex;
        [ObservableProperty] private string _density;

        public string AccentLabel => string.IsNullOrWhiteSpace(AccentHex) ? "Default accent" : AccentHex;

        // ---------------- Defaults ----------------

        [ObservableProperty] private string _defaultCopyFormat;
        [ObservableProperty] private string _defaultExportFormat;

        // ---------------- Hotkey ----------------

        [ObservableProperty] private string _hotkeyKey;
        [ObservableProperty] private bool _hotkeyCtrl;
        [ObservableProperty] private bool _hotkeyAlt;
        [ObservableProperty] private bool _hotkeyShift;
        [ObservableProperty] private bool _hotkeyMeta;

        public string HotkeyPreview =>
            $"Ctrl+Alt+P default · current: {(HotkeyCtrl ? "Ctrl+" : "")}{(HotkeyAlt ? "Alt+" : "")}" +
            $"{(HotkeyShift ? "Shift+" : "")}{(HotkeyMeta ? "Win+" : "")}{HotkeyKey}";

        // ---------------- Storage ----------------

        [ObservableProperty] private string _storageProvider;
        [ObservableProperty] private string? _storageDirectory;

        public string StorageLocationLabel =>
            string.IsNullOrWhiteSpace(StorageDirectory)
                ? "Default (%APPDATA%\\Spectrum)"
                : StorageDirectory;

        public SettingsViewModel()
        {
            var s = SettingsService.Settings;
            _theme = s.Theme;
            _accentHex = s.AccentHex;
            _density = s.Density;
            _defaultCopyFormat = s.DefaultCopyFormat;
            _defaultExportFormat = s.DefaultExportFormat;
            _hotkeyKey = s.HotkeyKey;
            _hotkeyCtrl = s.HotkeyCtrl;
            _hotkeyAlt = s.HotkeyAlt;
            _hotkeyShift = s.HotkeyShift;
            _hotkeyMeta = s.HotkeyMeta;
            _storageProvider = s.StorageProvider;
            _storageDirectory = s.StorageDirectory;
        }

        // ---------------- Apply + persist ----------------

        partial void OnThemeChanged(string value)
        {
            SettingsService.Settings.Theme = value;
            PersistAndApply();
        }

        partial void OnAccentHexChanged(string? value)
        {
            SettingsService.Settings.AccentHex = value;
            OnPropertyChanged(nameof(AccentLabel));
            PersistAndApply();
        }

        partial void OnDensityChanged(string value)
        {
            SettingsService.Settings.Density = value;
            PersistAndApply();
        }

        partial void OnDefaultCopyFormatChanged(string value)
        {
            SettingsService.Settings.DefaultCopyFormat = value;
            PushDefaultsToMainWindow();
            SettingsService.Save();
        }

        partial void OnDefaultExportFormatChanged(string value)
        {
            SettingsService.Settings.DefaultExportFormat = value;
            PushDefaultsToMainWindow();
            SettingsService.Save();
        }

        partial void OnHotkeyKeyChanged(string value)
        {
            SettingsService.Settings.HotkeyKey = value;
            RaiseHotkeyPreview();
            PersistAndApply();
        }

        partial void OnHotkeyCtrlChanged(bool value)
        {
            SettingsService.Settings.HotkeyCtrl = value;
            RaiseHotkeyPreview();
            PersistAndApply();
        }

        partial void OnHotkeyAltChanged(bool value)
        {
            SettingsService.Settings.HotkeyAlt = value;
            RaiseHotkeyPreview();
            PersistAndApply();
        }

        partial void OnHotkeyShiftChanged(bool value)
        {
            SettingsService.Settings.HotkeyShift = value;
            RaiseHotkeyPreview();
            PersistAndApply();
        }

        partial void OnHotkeyMetaChanged(bool value)
        {
            SettingsService.Settings.HotkeyMeta = value;
            RaiseHotkeyPreview();
            PersistAndApply();
        }

        partial void OnStorageProviderChanged(string value)
        {
            SettingsService.Settings.StorageProvider = value;
            SettingsService.Save();
            ApplyStorageToLibrary();
        }

        partial void OnStorageDirectoryChanged(string? value)
        {
            SettingsService.Settings.StorageDirectory = value;
            OnPropertyChanged(nameof(StorageLocationLabel));
            SettingsService.Save();
            ApplyStorageToLibrary();
        }

        private void RaiseHotkeyPreview() => OnPropertyChanged(nameof(HotkeyPreview));

        private void PersistAndApply()
        {
            SettingsService.Save();
            SettingsService.ApplyTheme();
            SettingsService.ApplyAccent();
            SettingsService.ApplyDensity();
            SettingsService.ApplyHotkey();
        }

        /// <summary>Swaps the library store (json ↔ sqlite / new folder) and reloads the sidebar.</summary>
        private void ApplyStorageToLibrary()
        {
            SettingsService.ApplyStorageLocation();

            PaletteLibraryService.ChangeStore(
                SettingsService.Settings.StorageProvider == "sqlite" && SqliteLibraryStore.IsAvailable
                    ? new SqliteLibraryStore(SettingsService.Settings.StorageDirectory)
                    : new JsonLibraryStore
                    {
                        DirectoryPath = SettingsService.Settings.StorageDirectory
                                       ?? System.IO.Path.Combine(
                                           Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                           "Spectrum"),
                    });

            MainLibrary?.ReloadFromService();
        }

        /// <summary>Set by the settings window so format defaults can reach the open board.</summary>
        public PaletteLibraryViewModel? MainLibrary { get; set; }

        private static void PushDefaultsToMainWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow?.DataContext is MainWindowViewModel vm)
            {
                vm.ApplyFormatDefaults();
            }
        }
    }
}
