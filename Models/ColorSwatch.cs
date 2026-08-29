using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace Spectrum.Models
{
    /// <summary>
    /// A single color in the palette. Commands are wired up by the
    /// owning ViewModel (see MainWindowViewModel.CreateSwatch) so the
    /// card in the UI can call back into copy/remove/reorder logic
    /// without needing a reference to the parent DataContext.
    /// </summary>
    public partial class ColorSwatch : ObservableObject
    {
        [ObservableProperty]
        private Color _color;

        [ObservableProperty]
        private string _name = "Color";

        public string Hex => $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";

        public IBrush Brush => new SolidColorBrush(Color);

        public ICommand? CopyCommand { get; set; }
        public ICommand? RemoveCommand { get; set; }
        public ICommand? MoveUpCommand { get; set; }
        public ICommand? MoveDownCommand { get; set; }

        partial void OnColorChanged(Color value)
        {
            OnPropertyChanged(nameof(Hex));
            OnPropertyChanged(nameof(Brush));
        }

        public ColorSwatch()
        {
        }

        public ColorSwatch(Color color, string name = "Color")
        {
            _color = color;
            _name = name;
        }
    }
}
