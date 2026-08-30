using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Spectrum.Models;
using Spectrum.ViewModels;
using System;

namespace Spectrum.Views
{
    public partial class MainWindow : Window
    {
        private Border? _draggedBorder;
        private ColorSwatch? _draggedSwatch;
        private Point _dragAnchor;
        private bool _isDragging;

        public MainWindow()
        {
            InitializeComponent();

            // Coolors-style shortcut: press Space anywhere in the window to
            // generate a new palette. Skipped while a TextBox has focus so
            // typing a name or hex value still works normally.
            AddHandler(KeyDownEvent, OnWindowKeyDown);
        }

        private void OnWindowKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space) return;

            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            if (focused is TextBox) return;

            if (DataContext is MainWindowViewModel vm && vm.GeneratePaletteCommand.CanExecute(null))
            {
                vm.GeneratePaletteCommand.Execute(null);
                e.Handled = true;
            }
        }

        // ---------------- Drag-to-reorder ----------------
        // Implemented with plain pointer tracking rather than Avalonia's
        // native DragDrop API: the board is a single horizontal row (a
        // UniformGrid), so "which slot is the pointer over" reduces to
        // simple index math against the track's width, and calling
        // Palette.Move as soon as the pointer crosses into a neighboring
        // slot gives an immediate, no-drop-target-needed reorder feel.
        // The Hand cursor itself is set declaratively in the swatch's XAML
        // (Cursor="Hand"), so it shows on hover even before a drag starts.

        private void OnSwatchPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Let the swatch's own buttons/toggle/rename box handle their
            // own clicks — only a bare press on the card background starts a drag.
            if (e.Source is Button or ToggleButton or TextBox) return;
            if (sender is not Border border || border.DataContext is not ColorSwatch swatch) return;
            if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;

            var track = border.FindAncestorOfType<UniformGrid>();
            if (track is null) return;

            _draggedBorder = border;
            _draggedSwatch = swatch;
            _isDragging = false;
            _dragAnchor = e.GetPosition(track);

            e.Pointer.Capture(border);
        }

        private void OnSwatchPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_draggedBorder is null || _draggedSwatch is null) return;
            if (!e.GetCurrentPoint(_draggedBorder).Properties.IsLeftButtonPressed) return;
            if (DataContext is not MainWindowViewModel vm) return;

            var track = _draggedBorder.FindAncestorOfType<UniformGrid>();
            if (track is null || track.Bounds.Width <= 0 || vm.Palette.Count == 0) return;

            var current = e.GetPosition(track);
            var delta = current - _dragAnchor;

            // Small threshold so an ordinary click doesn't register as a drag.
            if (!_isDragging && Math.Abs(delta.X) < 4 && Math.Abs(delta.Y) < 4) return;

            _isDragging = true;
            _draggedBorder.ZIndex = 100;
            _draggedBorder.Opacity = 0.85;
            _draggedBorder.RenderTransform = new TranslateTransform(delta.X, 0);

            var slotWidth = track.Bounds.Width / vm.Palette.Count;
            var targetIndex = Math.Clamp((int)(current.X / slotWidth), 0, vm.Palette.Count - 1);
            var currentIndex = vm.Palette.IndexOf(_draggedSwatch);

            if (targetIndex != currentIndex && currentIndex >= 0)
            {
                vm.Palette.Move(currentIndex, targetIndex);
                // The dragged card just jumped to a new slot along with its
                // item, so re-baseline the anchor and clear the transform —
                // otherwise the next delta would be measured from the old spot.
                _dragAnchor = current;
                _draggedBorder.RenderTransform = null;
            }
        }

        private void OnSwatchPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_draggedBorder is not null)
            {
                _draggedBorder.RenderTransform = null;
                _draggedBorder.ZIndex = 0;
                _draggedBorder.Opacity = 1.0;
                e.Pointer.Capture(null);
            }

            _draggedBorder = null;
            _draggedSwatch = null;
            _isDragging = false;
        }
    }
}