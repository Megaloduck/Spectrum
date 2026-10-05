using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Spectrum.Services;
using Spectrum.ViewModels;
using Spectrum.Views;
using System;
using System.Linq;

namespace Spectrum
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();

                // §10: settings must load (and apply storage location) before the
                // library initializes inside the MainWindowViewModel constructor.
                SettingsService.Load();
                SettingsService.ApplyStartup();

                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };

                // Density applies once the window exists (adds the "compact" class).
                SettingsService.ApplyDensity();

                // Make sure a pending debounced autosave is written before the
                // process exits (§9 autosave + crash recovery).
                desktop.Exit += (_, _) => PaletteLibraryService.Flush();

                // System tray icon + quick access (§7).
                SetupTrayIcon(desktop);
            }

            base.OnFrameworkInitializationCompleted();
        }

        // ---------------- System tray (§7 "System tray icon + quick access") ----------------

        private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                var trayIcon = new TrayIcon
                {
                    Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Spectrum/Assets/Spectrum.ico"))),
                    ToolTipText = "Spectrum — color palettes",
                };

                var show = new NativeMenuItem("Show Spectrum");
                show.Click += (_, _) => ShowMainWindow(desktop);

                var generate = new NativeMenuItem("New colors (Space)");
                generate.Click += (_, _) =>
                {
                    if (desktop.MainWindow?.DataContext is MainWindowViewModel vm &&
                        vm.Studio.GeneratePaletteCommand.CanExecute(null))
                    {
                        vm.Studio.GeneratePaletteCommand.Execute(null);
                    }
                };

                var theme = new NativeMenuItem("Toggle light / dark");
                theme.Click += (_, _) =>
                {
                    if (desktop.MainWindow?.DataContext is MainWindowViewModel vm &&
                        vm.ToggleThemeCommand.CanExecute(null))
                    {
                        vm.ToggleThemeCommand.Execute(null);
                    }
                };

                var separator = new NativeMenuItemSeparator();

                var exit = new NativeMenuItem("Exit");
                exit.Click += (_, _) => desktop.Shutdown();

                trayIcon.Menu = new NativeMenu
                {
                    Items = { show, generate, theme, separator, exit },
                };
                trayIcon.Clicked += (_, _) => ShowMainWindow(desktop);

                TrayIcon.SetIcons(this, new TrayIcons { trayIcon });
            }
            catch
            {
                // Some platforms/de session lack tray support — the app remains
                // fully usable without it.
            }
        }

        private static void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is not { } window) return;

            window.Show();
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;
            window.Activate();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}