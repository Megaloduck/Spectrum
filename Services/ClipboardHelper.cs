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
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is { } window)
            {
                var topLevel = TopLevel.GetTopLevel(window);
                if (topLevel?.Clipboard is { } clipboard)
                {
                    await clipboard.SetTextAsync(text);
                }
            }
        }
    }
}
