using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Spectrum.Models;
using Spectrum.ViewModels;
using System;
using System.ComponentModel;

namespace Spectrum.Views
{
    /// <summary>
    /// The shell: app bar, palette library and the host that shows the active
    /// workspace. Board/tab/dock behaviour lives in the workspace views, so what
    /// is left here is window-level — keyboard shortcuts, the command palette and
    /// the library column.
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindowViewModel? _subscribedVm;

        public MainWindow()
        {
            InitializeComponent();
            Closed += (_, _) =>
            {
                if (_subscribedVm is not null)
                {
                    _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
                    _subscribedVm.Library.RenameStarted -= OnPaletteRenameStarted;
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
                    _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
                    _subscribedVm.Library.RenameStarted -= OnPaletteRenameStarted;
                    _subscribedVm = null;
                }

                if (DataContext is MainWindowViewModel vm)
                {
                    _subscribedVm = vm;
                    vm.PropertyChanged += OnViewModelPropertyChanged;
                    vm.Library.RenameStarted += OnPaletteRenameStarted;
                }

                ApplyShellLayout();
            };
        }

        // ---------------- Collapsible library (§7) ----------------

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.LibraryVisible))
                ApplyShellLayout();
        }

        /// <summary>
        /// Sizes the library column. IsVisible alone would leave a 264px gutter,
        /// so the widths are driven here; the workspace handles its own dock.
        /// </summary>
        private void ApplyShellLayout()
        {
            var open = _subscribedVm is { LibraryVisible: true };

            var columns = BodyGrid.ColumnDefinitions;
            columns[0].Width = new GridLength(open ? 264 : 0);
            columns[1].Width = new GridLength(open ? 12 : 0);
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
                        RunIfPossible(vm.Studio.PickFromScreenCommand);
                        break;
                    case Key.E:
                        RunIfPossible(vm.Export.ExportToFileCommand);
                        break;
                    case Key.J:
                        RunIfPossible(vm.Export.ExportPngCommand);
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
                    RunIfPossible(vm.Studio.GeneratePaletteCommand);
                    e.Handled = true;
                    break;
                case Key.Delete:
                    if (vm.SelectedSwatch is not null) RunIfPossible(vm.RemoveSwatchCommand, vm.SelectedSwatch);
                    e.Handled = true;
                    break;
                case Key.Escape:
                    vm.SelectedSwatch = null;
                    break;
                case Key.F2:
                    RunIfPossible(vm.Library.RenamePaletteCommand, vm.Library.SelectedPalette);
                    e.Handled = true;
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

        private void OnPaletteItemPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Double-click on a palette row (or its name) opens inline rename.
            if (e.ClickCount != 2) return;

            var item = (sender as Control)?.DataContext as PaletteItemViewModel;
            if (item is null) return;

            if (DataContext is MainWindowViewModel vm)
                vm.Library.RenamePaletteCommand.Execute(item);

            e.Handled = true;
        }

        /// <summary>
        /// The library ViewModel can flip a row into rename mode but cannot touch
        /// controls, so it raises RenameStarted and this focuses (and selects) the
        /// editor — whichever way rename was started: double-click, the pencil
        /// button, F2 or the command palette.
        /// </summary>
        private void OnPaletteRenameStarted(PaletteItemViewModel item)
        {
            // The TextBox only exists once the row's containers have re-rendered
            // with IsRenaming = true, so wait for the next layout/render tick.
            Dispatcher.UIThread.Post(() =>
            {
                if (!item.IsRenaming) return;

                var container = FindContainer(item);
                if (FindDescendantByName(container, "NameEditor") is not TextBox editor) return;

                editor.Focus();
                editor.SelectAll();
            }, DispatcherPriority.Render);
        }

        private ListBoxItem? FindContainer(PaletteItemViewModel item)
        {
            foreach (var descendant in PaletteList.GetVisualDescendants())
            {
                if (descendant is ListBoxItem box && ReferenceEquals(box.DataContext, item))
                    return box;
            }
            return null;
        }

        private static Control? FindDescendantByName(Visual? root, string name)
        {
            if (root is null) return null;
            if (root is Control c && c.Name == name) return c;
            foreach (var child in root.GetVisualChildren())
            {
                var found = FindDescendantByName(child, name);
                if (found is not null) return found;
            }
            return null;
        }

        private void OnPaletteNamePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Let the ListBox handle single clicks for selection, but double-clicks
            // still open rename via the row handler above.
            if (e.ClickCount == 2)
            {
                OnPaletteItemPointerPressed(sender, e);
            }
        }

        private void OnNameEditorLostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is not TextBox editor) return;
            var item = editor.DataContext as PaletteItemViewModel;
            item?.EndRename();
        }

        private void OnNameEditorKeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not TextBox editor) return;
            var item = editor.DataContext as PaletteItemViewModel;

            if (e.Key == Key.Enter)
            {
                // The TwoWay binding already wrote the text back to
                // PaletteItemViewModel.Name, which pushed it into the DTO.
                item?.EndRename();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // Revert: the editor is two-way bound with PropertyChanged, so the
                // typed text already reached the DTO — put the original back.
                item?.CancelRename();
                e.Handled = true;
            }
        }
    }
}
