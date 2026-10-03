using Avalonia.Controls;
using Avalonia.Interactivity;
using Spectrum.Services;
using Spectrum.ViewModels;
using System;
using System.Threading.Tasks;

namespace Spectrum.Views
{
    /// <summary>§10 Settings dialog. All controls bind straight to SettingsViewModel,
    /// which persists + applies on every change.</summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _vm;
        private bool _resetArmed;

        public SettingsWindow()
        {
            InitializeComponent();

            _vm = new SettingsViewModel();
            DataContext = _vm;

            VersionText.Text = $"Spectrum {UpdateService.CurrentVersion} · .NET 9 · Avalonia 11";
        }

        /// <summary>Used by the main window so storage changes can reload the sidebar.</summary>
        public void AttachLibrary(PaletteLibraryViewModel library) => _vm.MainLibrary = library;

        private async void OnPickAccent(object? sender, RoutedEventArgs e)
        {
            var initial = Avalonia.Media.Color.FromRgb(0x3B, 0x82, 0xF6);
            if (!string.IsNullOrWhiteSpace(_vm.AccentHex) && ColorMathService.TryParseHex(_vm.AccentHex, out var current))
                initial = current;

            var dialog = new ColorPickerDialog(initial);
            var picked = await dialog.ShowDialog<Avalonia.Media.Color?>(this);
            if (picked is null) return;

            _vm.AccentHex = ColorMathService.ToHex(picked.Value);
        }

        private void OnResetAccent(object? sender, RoutedEventArgs e) => _vm.AccentHex = null;

        private async void OnBrowseStorage(object? sender, RoutedEventArgs e)
        {
            var storage = StorageProvider;
            var folders = await storage.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Choose the Spectrum storage folder",
                AllowMultiple = false,
            });

            if (folders.Count > 0)
            {
                _vm.StorageDirectory = folders[0].Path.LocalPath;
            }
        }

        /// <summary>Two-click confirmation: no accidental data loss.</summary>
        private async void OnResetData(object? sender, RoutedEventArgs e)
        {
            if (!_resetArmed)
            {
                _resetArmed = true;
                ResetButton.Content = "Click again to delete EVERYTHING";
                return;
            }

            PaletteLibraryService.Reset();
            SettingsService.ApplyStorageLocation();
            PaletteLibraryService.ChangeStore(
                SettingsService.Settings.StorageProvider == "sqlite" && SqliteLibraryStore.IsAvailable
                    ? new SqliteLibraryStore(SettingsService.Settings.StorageDirectory)
                    : new JsonLibraryStore());

            _vm.MainLibrary?.ReloadFromService();
            if (PaletteLibraryService.Library.Palettes.Count == 0)
                _vm.MainLibrary?.CreateDefaultPalette();

            ResetButton.Content = "All palettes deleted.";
            await Task.CompletedTask;
        }

        private void OnDone(object? sender, RoutedEventArgs e) => Close();

        /// <summary>§11 update check — explicitly user-triggered, never backgrounded.</summary>
        private async void OnCheckUpdates(object? sender, RoutedEventArgs e)
        {
            UpdateStatus.Text = "Checking…";
            try
            {
                var update = await UpdateService.CheckForUpdatesAsync();
                UpdateStatus.Text = update is null
                    ? $"You're up to date ({UpdateService.CurrentVersion})."
                    : $"Update available: {update.Version} — {update.Notes ?? "see release notes"}";
            }
            catch
            {
                UpdateStatus.Text =
                    "Couldn't check (offline or no release channel configured yet). " +
                    "Spectrum works fully offline either way.";
            }
        }
    }
}
