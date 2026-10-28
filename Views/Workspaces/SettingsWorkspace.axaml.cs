using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Spectrum.Services;
using Spectrum.ViewModels;

namespace Spectrum.Views.Workspaces
{
    /// <summary>
    /// Ctrl+, · Settings as a workspace. The options themselves bind straight to
    /// <see cref="SettingsViewModel"/>, which persists and applies on change; only
    /// the two actions that need a native dialog live here.
    /// </summary>
    public partial class SettingsWorkspace : UserControl
    {
        public SettingsWorkspace() => InitializeComponent();

        private SettingsViewModel? Vm => (DataContext as MainWindowViewModel)?.Settings;

        private async void OnPickAccent(object? sender, RoutedEventArgs e)
        {
            if (Vm is not { } vm || MainWindowViewModel.GetOwnerWindow() is not { } owner) return;

            var initial = Avalonia.Media.Color.FromRgb(0x3B, 0x82, 0xF6);
            if (!string.IsNullOrWhiteSpace(vm.AccentHex) &&
                ColorMathService.TryParseHex(vm.AccentHex, out var current))
            {
                initial = current;
            }

            var picked = await new ColorPickerDialog(initial).ShowDialog<Avalonia.Media.Color?>(owner);
            if (picked is null) return;

            vm.AccentHex = ColorMathService.ToHex(picked.Value);
        }

        private void OnResetAccent(object? sender, RoutedEventArgs e)
        {
            if (Vm is { } vm) vm.AccentHex = null;
        }

        private async void OnBrowseStorage(object? sender, RoutedEventArgs e)
        {
            if (Vm is not { } vm || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;

            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose the Spectrum storage folder",
                AllowMultiple = false,
            });

            if (folders.Count > 0)
            {
                vm.StorageDirectory = folders[0].Path.LocalPath;
            }
        }
    }
}
