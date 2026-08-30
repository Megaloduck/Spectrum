using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Spectrum.Services
{
    /// <summary>
    /// Wraps Avalonia's storage provider so the ViewModel can prompt for a
    /// file location without taking a direct dependency on Window/TopLevel.
    /// </summary>
    public static class PaletteFileService
    {
        private const string FileTypeName = "Spectrum Palette (*.json)";

        public static async Task<string?> PickSaveFileAsync(string suggestedFileName)
        {
            var storage = GetStorageProvider();
            if (storage is null) return null;

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Palette",
                SuggestedFileName = suggestedFileName,
                DefaultExtension = "json",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new(FileTypeName) { Patterns = new[] { "*.json" } }
                }
            });

            return file?.Path.LocalPath;
        }

        public static async Task<string?> PickOpenFileAsync()
        {
            var storage = GetStorageProvider();
            if (storage is null) return null;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Palette",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new(FileTypeName) { Patterns = new[] { "*.json" } }
                }
            });

            return files.Count > 0 ? files[0].Path.LocalPath : null;
        }

        private static IStorageProvider? GetStorageProvider()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is { } window)
            {
                return TopLevel.GetTopLevel(window)?.StorageProvider;
            }

            return null;
        }
    }
}
