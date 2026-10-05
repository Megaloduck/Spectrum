using Avalonia.Controls;
using Avalonia.Media;
using Spectrum.Models;
using Spectrum.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Spectrum.Views
{
    /// <summary>§8 "Side-by-side palette comparison": current palette vs any palette in the library.</summary>
    public partial class CompareWindow : Window
    {
        public sealed class Row
        {
            public Row(Color color, string name)
            {
                Brush = new SolidColorBrush(color);
                Hex = ColorMathService.ToHex(color);
                Name = name;
                var ratio = ContrastService.ContrastRatio(color, Colors.White);
                Contrast = $"{ContrastService.Rate(ratio)} {ratio:N1}:1";
            }

            public IBrush Brush { get; }
            public string Hex { get; }
            public string Name { get; }
            public string Contrast { get; }
        }

        private readonly PaletteDto? _left;
        private readonly List<PaletteDto> _allPalettes = new();

        /// <summary>Design-time only — the real window is created with a palette.</summary>
        public CompareWindow()
        {
            InitializeComponent();
        }

        public CompareWindow(PaletteDto? activePalette) : this()
        {

            _left = activePalette;
            _allPalettes = PaletteLibraryService.Library.Palettes;

            if (_left is not null)
            {
                LeftTitle.Text = _left.Name;
                LeftCount.Text = _left.Swatches.Count == 1 ? "1 color" : $"{_left.Swatches.Count} colors";
                LeftColumn.ItemsSource = BuildRows(_left);
            }

            PalettePicker.ItemsSource = _allPalettes;
            var initial = _allPalettes.FirstOrDefault(p => p.Id != _left?.Id);
            if (initial is not null)
            {
                PalettePicker.SelectedItem = initial;
                ShowRight(initial);
            }
        }

        private void OnPickerChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (PalettePicker.SelectedItem is PaletteDto palette)
                ShowRight(palette);
        }

        private void ShowRight(PaletteDto palette) => RightColumn.ItemsSource = BuildRows(palette);

        private static List<Row> BuildRows(PaletteDto palette) =>
            palette.Swatches.Select(s => new Row(Color.FromArgb(255, s.R, s.G, s.B), s.Name)).ToList();
    }
}
