using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Spectrum.Models;
using Spectrum.ViewModels;

namespace Spectrum.Views
{
    public partial class MainWindow : Window
    {
        // Minimum pointer travel (in pixels) before a press on a swatch card
        // is treated as a drag rather than a click.
        private const double DragThreshold = 6;

        private ColorSwatch? _dragCandidate;
        private Point _dragStartPoint;

        public MainWindow()
        {
            InitializeComponent();

            DragDrop.SetAllowDrop(PaletteItemsControl, true);
            PaletteItemsControl.AddHandler(PointerPressedEvent, PaletteItems_PointerPressed);
            PaletteItemsControl.AddHandler(PointerMovedEvent, PaletteItems_PointerMoved);
            PaletteItemsControl.AddHandler(DragDrop.DragOverEvent, PaletteItems_DragOver);
            PaletteItemsControl.AddHandler(DragDrop.DropEvent, PaletteItems_Drop);
        }

        // Walks up from whatever visual was hit to find the ColorSwatch
        // backing its containing card.
        private static ColorSwatch? FindSwatch(object? source)
        {
            var current = source as Visual;
            while (current is not null)
            {
                if (current is StyledElement { DataContext: ColorSwatch swatch })
                {
                    return swatch;
                }

                current = current.GetVisualParent();
            }

            return null;
        }

        private void PaletteItems_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Ignore presses on interactive controls (buttons, toggles, the
            // rename textbox) so clicking them keeps working normally
            // instead of being hijacked into starting a drag.
            if (e.Source is Button or RadioButton or TextBox) return;
            if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;

            _dragCandidate = FindSwatch(e.Source);
            _dragStartPoint = e.GetPosition(null);
        }

        private async void PaletteItems_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragCandidate is null) return;

            if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                _dragCandidate = null;
                return;
            }

            var current = e.GetPosition(null);
            var dx = current.X - _dragStartPoint.X;
            var dy = current.Y - _dragStartPoint.Y;
            if (dx * dx + dy * dy < DragThreshold * DragThreshold) return;

            var swatch = _dragCandidate;
            _dragCandidate = null;

            var data = new DataObject();
            data.Set(nameof(ColorSwatch), swatch!);
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        }

        private void PaletteItems_DragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = e.Data.Contains(nameof(ColorSwatch)) ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void PaletteItems_Drop(object? sender, DragEventArgs e)
        {
            if (e.Data.Get(nameof(ColorSwatch)) is not ColorSwatch source) return;

            var target = FindSwatch(e.Source);
            if (target is null || ReferenceEquals(source, target)) return;

            if (DataContext is MainWindowViewModel vm)
            {
                vm.ReorderSwatch(source, target);
            }
        }
    }
}
