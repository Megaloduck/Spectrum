using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
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

        private MainWindowViewModel? _subscribedVm;

        public MainWindow()
        {
            InitializeComponent();
            Closed += (_, _) =>
            {
                if (_subscribedVm is not null)
                {
                    _subscribedVm.ToggleLibraryRequested -= OnToggleLibrary;
                    _subscribedVm.ToggleInspectorRequested -= OnToggleInspector;
                    _subscribedVm.ViewModeRequested -= OnViewModeRequested;
                    _subscribedVm.WorkspaceModeChanged -= ApplyShellLayout;
                    _subscribedVm = null;
                }
                (DataContext as MainWindowViewModel)?.Detach();
            };

            // Coolors-style shortcut handling (full map in OnWindowKeyDown).
            AddHandler(KeyDownEvent, OnWindowKeyDown);

            DataContextChanged += (_, _) =>
            {
                if (_subscribedVm is not null)
                {
                    _subscribedVm.ToggleLibraryRequested -= OnToggleLibrary;
                    _subscribedVm.ToggleInspectorRequested -= OnToggleInspector;
                    _subscribedVm.ViewModeRequested -= OnViewModeRequested;
                    _subscribedVm.WorkspaceModeChanged -= ApplyShellLayout;
                    _subscribedVm = null;
                }

                if (DataContext is MainWindowViewModel vm)
                {
                    _subscribedVm = vm;
                    vm.ToggleLibraryRequested += OnToggleLibrary;
                    vm.ToggleInspectorRequested += OnToggleInspector;
                    vm.ViewModeRequested += OnViewModeRequested;
                    vm.WorkspaceModeChanged += ApplyShellLayout;
                    ApplyViewMode(vm.ViewMode);
                    ApplyShellLayout();
                }
            };
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

        // ---------------- Collapsible panels (§7) ----------------

        private bool _libraryVisible = true;
        private bool _inspectorVisible = true;

        private void OnToggleLibrary() => SetPanelVisibility(
            library: !_libraryVisible, inspector: _inspectorVisible);

        private void OnToggleInspector() => SetPanelVisibility(
            library: _libraryVisible, inspector: !_inspectorVisible);

        private void SetPanelVisibility(bool library, bool inspector)
        {
            _libraryVisible = library;
            _inspectorVisible = inspector;
            ApplyShellLayout();
        }

        /// <summary>
        /// Decides which of the three contextual dock panels is on screen (only one
        /// workspace is ever active) and re-measures the shell columns. Export runs
        /// full-width, so the dock and its splitter step aside entirely.
        /// </summary>
        private void ApplyShellLayout()
        {
            if (_subscribedVm is null) return;
            var vm = _subscribedVm;

            var dockOpen = _inspectorVisible && vm.WorkspaceMode != WorkspaceMode.Export;

            LibraryPanel.IsVisible = _libraryVisible;
            LibrarySplitter.IsVisible = _libraryVisible;

            EditDock.IsVisible = dockOpen && vm.IsStudioMode;
            PreviewDock.IsVisible = dockOpen && vm.IsPreviewMode;
            AnalyzeDock.IsVisible = dockOpen && vm.IsAnalyzeMode;
            InspectorSplitter.IsVisible = dockOpen;

            var columns = BodyGrid.ColumnDefinitions;
            columns[0].Width = new GridLength(_libraryVisible ? 264 : 0);
            columns[1].Width = new GridLength(_libraryVisible ? 12 : 0);
            columns[3].Width = new GridLength(dockOpen ? 12 : 0);
            columns[4].Width = new GridLength(dockOpen ? 330 : 0);
        }

        private void OnWindowKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            var typing = focused is TextBox;

            // Command palette opens even from inside a text box (§7).
            if (ctrl && e.Key == Key.K)
            {
                OpenCommandPalette(vm);
                e.Handled = true;
                return;
            }

            // Don't steal ordinary typing.
            if (typing) return;

            if (ctrl)
            {
                switch (e.Key)
                {
                    case Key.Z when !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                        RunIfPossible(vm.UndoCommand);
                        break;
                    case Key.Z:
                    case Key.Y:
                        RunIfPossible(vm.RedoCommand);
                        break;
                    case Key.S:
                        RunIfPossible(vm.SavePaletteCommand);
                        break;
                    case Key.O:
                        RunIfPossible(vm.LoadPaletteCommand);
                        break;
                    case Key.N:
                        RunIfPossible(vm.Library.NewPaletteCommand);
                        break;
                    case Key.D:
                        if (vm.SelectedSwatch is not null) RunIfPossible(vm.DuplicateSwatchCommand, vm.SelectedSwatch);
                        break;
                    case Key.P:
                        RunIfPossible(vm.PickFromScreenCommand);
                        break;
                    case Key.E:
                        RunIfPossible(vm.ExportToFileCommand);
                        break;
                    case Key.J:
                        RunIfPossible(vm.ExportPngCommand);
                        break;
                    case Key.B:
                        RunIfPossible(vm.ToggleLibraryPanelCommand);
                        break;
                    case Key.I:
                        RunIfPossible(vm.ToggleInspectorCommand);
                        break;
                    case Key.OemComma:
                        RunIfPossible(vm.OpenSettingsCommand);
                        break;
                    case Key.D1:
                        RunIfPossible(vm.SetWorkspaceCommand, WorkspaceMode.Studio);
                        break;
                    case Key.D2:
                        RunIfPossible(vm.SetWorkspaceCommand, WorkspaceMode.Preview);
                        break;
                    case Key.D3:
                        RunIfPossible(vm.SetWorkspaceCommand, WorkspaceMode.Analyze);
                        break;
                    case Key.D4:
                        RunIfPossible(vm.SetWorkspaceCommand, WorkspaceMode.Export);
                        break;
                    default:
                        return;
                }

                e.Handled = true;
                return;
            }

            switch (e.Key)
            {
                case Key.Space:
                    RunIfPossible(vm.GeneratePaletteCommand);
                    e.Handled = true;
                    break;
                case Key.Delete:
                    if (vm.SelectedSwatch is not null) RunIfPossible(vm.RemoveSwatchCommand, vm.SelectedSwatch);
                    e.Handled = true;
                    break;
                case Key.Escape:
                    vm.SelectedSwatch = null;
                    break;
            }
        }

        private static void RunIfPossible(System.Windows.Input.ICommand command, object? parameter = null)
        {
            if (command.CanExecute(parameter)) command.Execute(parameter);
        }

        private async void OpenCommandPalette(MainWindowViewModel vm)
        {
            var palette = new CommandPaletteWindow(vm);
            await palette.ShowDialog(this);
        }

        private void OnToggleLibraryClick(object? sender, RoutedEventArgs e) => OnToggleLibrary();

        private void OnToggleInspectorClick(object? sender, RoutedEventArgs e) => OnToggleInspector();

        // ---------------- Tabs (§7) ----------------

        private void OnTabPressed(object? sender, PointerPressedEventArgs e)
        {
            // The tab's close button has its own handler; don't also activate.
            if (e.Source is Button) return;
            if (sender is Border { DataContext: PaletteItemViewModel item } &&
                DataContext is MainWindowViewModel vm)
            {
                vm.Library.SelectedPalette = item;
            }
        }

        private void OnTabClose(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: PaletteItemViewModel item } &&
                DataContext is MainWindowViewModel vm &&
                vm.Library.CloseTabCommand.CanExecute(item))
            {
                vm.Library.CloseTabCommand.Execute(item);
            }
        }

        /// <summary>Clicking the big base-color preview opens the spectrum picker (§2).</summary>
        private void OnBasePreviewPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm && vm.OpenColorPickerCommand.CanExecute(null))
            {
                vm.OpenColorPickerCommand.Execute(null);
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