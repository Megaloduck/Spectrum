using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Spectrum.Services
{
    /// <summary>
    /// Opens a native "pick an image" dialog and decodes the result, using the
    /// same desktop-lifetime lookup pattern as ClipboardHelper.
    /// </summary>
    public static class FilePickerHelper
    {
        public static async Task<Bitmap?> PickImageAsync()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
                || desktop.MainWindow is not { } window)
            {
                return null;
            }

            var topLevel = TopLevel.GetTopLevel(window);
            if (topLevel?.StorageProvider is not { } storageProvider)
            {
                return null;
            }

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Extract colors from image",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Images")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" }
                    }
                }
            });

            if (files.Count == 0) return null;

            await using var stream = await files[0].OpenReadAsync();
            return new Bitmap(stream);
        }
    }
}   