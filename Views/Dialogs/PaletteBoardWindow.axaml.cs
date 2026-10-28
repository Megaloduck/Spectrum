using Avalonia.Controls;

namespace Spectrum.Views
{
    /// <summary>
    /// A floating palette board. The shell's ViewModel is handed in as the
    /// DataContext, so this window is a live second view of the same palette
    /// rather than a second copy of the app.
    /// </summary>
    public partial class PaletteBoardWindow : Window
    {
        public PaletteBoardWindow() => InitializeComponent();
    }
}
