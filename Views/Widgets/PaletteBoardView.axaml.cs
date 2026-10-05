using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Spectrum.Models;
using Spectrum.ViewModels;
using System;

namespace Spectrum.Views
{
    /// <summary>
    /// The swatch board: the four card templates and the drag-to-reorder
    /// behaviour. Owned here rather than by the window so that Studio, Preview
    /// and Analyze can each host a board without duplicating it.
    /// </summary>
    public partial class PaletteBoardView : UserControl
    {
        private MainWindowViewModel? _subscribedVm;

        public PaletteBoardView() => InitializeComponent();

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (_subscribedVm is not null)
            {
                _subscribedVm.ViewModeRequested -= OnViewModeRequested;
                _subscribedVm = null;
            }

            if (DataContext is MainWindowViewModel vm)
            {
                _subscribedVm = vm;
                vm.ViewModeRequested += OnViewModeRequested;
                ApplyViewMode(vm.ViewMode);
            }
        }

        // ---------------- View modes (§8 grid / list / compact) ----------------

        private void OnViewModeRequested()
        {
            if (_subscribedVm is not null) ApplyViewMode(_subscribedVm.ViewMode);
        }

        private void ApplyViewMode(ViewMode mode)
        {
            var (cardKey, panelKey) = mode switch
            {
                ViewMode.Grid => ("GridCardTemplate", "GridPanel"),
                ViewMode.List => ("ListCardTemplate", "ListPanel"),
                ViewMode.Compact => ("CompactCardTemplate", "CompactPanel"),
                _ => ("RowCardTemplate", "RowPanel"),
            };

            Board.ItemTemplate = (IDataTemplate)Resources[cardKey]!;

            // ItemsPanel's template type isn't publicly nameable in this Avalonia
            // version, so assign it through the property's own type.
            typeof(ItemsControl).GetProperty(nameof(ItemsControl.ItemsPanel))!
                .SetValue(Board, Resources[panelKey]);
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

        private Border? _draggedBorder;
        private ColorSwatch? _draggedSwatch;
        private Point _dragAnchor;
        private bool _isDragging;

        private void OnSwatchPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Let the swatch's own buttons/toggle/rename box handle their
            // own clicks — only a bare press on the card background starts a drag.
            if (e.Source is Button or ToggleButton or TextBox) return;
            if (sender is not Border border || border.DataContext is not ColorSwatch swatch) return;
            if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;

            // Track the clicked swatch for shortcuts (Ctrl+D / Delete) and §8 zoom.
            if (DataContext is MainWindowViewModel selectVm) selectVm.SelectedSwatch = swatch;

            var panel = border.FindAncestorOfType<Panel>();
            if (panel is null) return;

            _draggedBorder = border;
            _draggedSwatch = swatch;
            _isDragging = false;
            _dragAnchor = e.GetPosition(panel);

            e.Pointer.Capture(border);
        }

        private void OnSwatchPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_draggedBorder is null || _draggedSwatch is null) return;
            if (!e.GetCurrentPoint(_draggedBorder).Properties.IsLeftButtonPressed) return;
            if (DataContext is not MainWindowViewModel vm) return;

            var panel = _draggedBorder.FindAncestorOfType<Panel>();
            if (panel is null || vm.Palette.Count == 0) return;

            var current = e.GetPosition(panel);
            var delta = current - _dragAnchor;

            // Small threshold so an ordinary click doesn't register as a drag.
            if (!_isDragging && Math.Abs(delta.X) < 4 && Math.Abs(delta.Y) < 4) return;

            _isDragging = true;
            _draggedBorder.ZIndex = 100;
            _draggedBorder.Opacity = 0.85;
            _draggedBorder.RenderTransform = new TranslateTransform(delta.X, delta.Y);

            // Hit-test by container bounds instead of slot math — this works for
            // every layout (single-row UniformGrid, wrapped grid, vertical list,
            // compact chips) without knowing anything about the panel's geometry.
            var targetIndex = HitTestIndex(panel, current);
            var currentIndex = vm.Palette.IndexOf(_draggedSwatch);

            if (targetIndex >= 0 && targetIndex != currentIndex && currentIndex >= 0)
            {
                vm.Palette.Move(currentIndex, targetIndex);
                // The dragged card just jumped to a new slot along with its
                // item, so re-baseline the anchor and clear the transform —
                // otherwise the next delta would be measured from the old spot.
                _dragAnchor = current;
                _draggedBorder.RenderTransform = null;
            }
        }

        private static int HitTestIndex(Panel panel, Point position)
        {
            for (var i = 0; i < panel.Children.Count; i++)
            {
                if (panel.Children[i].Bounds.Contains(position)) return i;
            }

            return -1;
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
