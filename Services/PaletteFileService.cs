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
        public static async Task<string?> PickSaveFileAsync(
            string suggestedFileName,
            string extension = "json",
            string fileTypeLabel = "Spectrum Palette (*.json)")
        {
            var storage = GetStorageProvider();
            if (storage is null) return null;

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save",
                SuggestedFileName = suggestedFileName,
                DefaultExtension = extension,
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new(fileTypeLabel) { Patterns = new[] { "*." + extension } }
                }
            });

            return file?.Path.LocalPath;
        }

        public static async Task<string?> PickOpenFileAsync(
            string fileTypeLabel = "Spectrum Palette (*.json)",
            string pattern = "*.json",
            string title = "Open Palette")
        {
            var storage = GetStorageProvider();
            if (storage is null) return null;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new(fileTypeLabel) { Patterns = new[] { pattern } }
                }
            });

            return files.Count > 0 ? files[0].Path.LocalPath : null;
        }

        public static async Task<string?> PickOpenFileAsync(
            string fileTypeLabel,
            string[] patterns,
            string title)
        {
            var storage = GetStorageProvider();
            if (storage is null) return null;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new(fileTypeLabel) { Patterns = patterns }
                }
            });

            return files.Count > 0 ? files[0].Path.LocalPath : null;
        }

        /// <summary>Native save dialog for any file type list (PNG/SVG export, etc.).</summary>
        public static async Task<string?> PickSaveFileAsync(string suggestedFileName, params FilePickerFileType[] types)
        {
            var storage = GetStorageProvider();
            if (storage is null) return null;

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export",
                SuggestedFileName = suggestedFileName,
                FileTypeChoices = types.Length > 0 ? types : null,
            });

            return file?.Path.LocalPath;
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
