using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Threading.Tasks;

namespace Spectrum.Services
{
    public static class ClipboardHelper
    {
        public static async Task SetTextAsync(string text)
        {
            if (TopLevelFromMain() is { } topLevel && topLevel.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(text);
            }
        }

        /// <summary>Reads the current text clipboard content, or null when unavailable (§2 paste).</summary>
        public static async Task<string?> GetTextAsync()
        {
            if (TopLevelFromMain() is { } topLevel && topLevel.Clipboard is { } clipboard)
            {
                try
                {
                    return await clipboard.GetTextAsync();
                }
                catch
                {
                    // Clipboard can be locked by another process — treat as empty.
                    return null;
                }
            }

            return null;
        }

        private static TopLevel? TopLevelFromMain()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is { } window)
            {
                return TopLevel.GetTopLevel(window);
            }

            return null;
        }
    }
}
