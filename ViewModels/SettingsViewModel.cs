using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spectrum.Models;
using Spectrum.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

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

        // ---------------- About ----------------

        public string VersionLabel => $"Spectrum {UpdateService.CurrentVersion} · .NET 9 · Avalonia 11";

        [ObservableProperty]
        private string _updateStatus =
            "Offline-first: Spectrum makes no network calls in the background.";

        /// <summary>§11 update check — explicitly user-triggered, never backgrounded.</summary>
        [RelayCommand]
        private async Task CheckForUpdatesAsync()
        {
            UpdateStatus = "Checking…";
            try
            {
                var update = await UpdateService.CheckForUpdatesAsync();
                UpdateStatus = update is null
                    ? $"You're up to date ({UpdateService.CurrentVersion})."
                    : $"Update available: {update.Version} — {update.Notes ?? "see release notes"}";
            }
            catch
            {
                UpdateStatus =
                    "Couldn't check (offline or no release channel configured yet). " +
                    "Spectrum works fully offline either way.";
            }
        }

        // ---------------- Danger zone ----------------

        /// <summary>First click arms the button, the second one deletes — no accidental
        /// data loss, and the state lives here so the view stays declarative.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ResetButtonLabel))]
        private bool _resetArmed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasResetStatus))]
        private string _resetStatus = string.Empty;

        public string ResetButtonLabel =>
            ResetArmed ? "Click again to delete EVERYTHING" : "Delete all palettes…";

        public bool HasResetStatus => !string.IsNullOrEmpty(ResetStatus);

        /// <summary>Leaving the workspace drops an armed delete, so a later click
        /// can never be mistaken for the first one.</summary>
        public void DisarmReset()
        {
            ResetArmed = false;
            ResetStatus = string.Empty;
        }

        [RelayCommand]
        private void ResetData()
        {
            if (!ResetArmed)
            {
                ResetArmed = true;
                ResetStatus = "Click again to delete every palette, version and backup.";
                return;
            }

            PaletteLibraryService.Reset();
            SettingsService.ApplyStorageLocation();
            PaletteLibraryService.ChangeStore(
                SettingsService.Settings.StorageProvider == "sqlite" && SqliteLibraryStore.IsAvailable
                    ? new SqliteLibraryStore(SettingsService.Settings.StorageDirectory)
                    : new JsonLibraryStore());

            MainLibrary?.ReloadFromService();
            if (PaletteLibraryService.Library.Palettes.Count == 0)
                MainLibrary?.CreateDefaultPalette();

            ResetArmed = false;
            ResetStatus = "All palettes deleted.";
        }

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

        // Enum-typed so the ComboBoxes can actually match their ItemsSource
        // (the persisted form stays a string).
        [ObservableProperty] private CopyFormat _defaultCopyFormat = CopyFormat.Hex;
        [ObservableProperty] private ExportFormat _defaultExportFormat = ExportFormat.Json;

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
            if (Enum.TryParse<CopyFormat>(s.DefaultCopyFormat, out var copyFormat))
                _defaultCopyFormat = copyFormat;
            if (Enum.TryParse<ExportFormat>(s.DefaultExportFormat, out var exportFormat))
                _defaultExportFormat = exportFormat;
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

        partial void OnDefaultCopyFormatChanged(CopyFormat value)
        {
            SettingsService.Settings.DefaultCopyFormat = value.ToString();
            PushDefaultsToMainWindow();
            SettingsService.Save();
        }

        partial void OnDefaultExportFormatChanged(ExportFormat value)
        {
            SettingsService.Settings.DefaultExportFormat = value.ToString();
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

        /// <summary>Set by the shell so storage changes can reload the sidebar.</summary>
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
