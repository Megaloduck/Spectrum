using Avalonia;
using Avalonia.Controls;
using Spectrum.ViewModels;
using System;
using System.ComponentModel;

namespace Spectrum.Views
{
    /// <summary>
    /// Base class for the workspaces that own a right-hand dock (Studio, Preview
    /// and Analyze). Each of them is a grid of <c>center · splitter · dock</c>, and
    /// the only layout logic they share is collapsing/restoring that dock column
    /// when Ctrl+I toggles <see cref="MainWindowViewModel.InspectorVisible"/> —
    /// so it lives here once instead of being repeated three times.
    /// </summary>
    public abstract class DockedWorkspaceView : UserControl
    {
        /// <summary>The workspace's root grid: columns center · splitter · dock.</summary>
        protected abstract Grid RootGrid { get; }

        /// <summary>Index of the dock's GridSplitter column.</summary>
        protected virtual int SplitterColumn => 1;

        /// <summary>Index of the dock panel column.</summary>
        protected virtual int DockColumn => 2;

        private const double SplitterWidth = 12;
        private const double DockWidth = 330;

        private MainWindowViewModel? _subscribedVm;

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (_subscribedVm is not null)
            {
                _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
                _subscribedVm = null;
            }

            if (DataContext is MainWindowViewModel vm)
            {
                _subscribedVm = vm;
                vm.PropertyChanged += OnViewModelPropertyChanged;
            }

            ApplyDockLayout();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.InspectorVisible))
                ApplyDockLayout();
        }

        /// <summary>
        /// Zero the splitter/dock columns when the dock is hidden and restore them
        /// when it comes back. IsVisible alone would leave an empty 330px gutter,
        /// so the column widths are driven here too.
        /// </summary>
        protected void ApplyDockLayout()
        {
            if (RootGrid is not { } root) return;

            var columns = root.ColumnDefinitions;
            if (columns.Count <= DockColumn) return;

            var open = DataContext is MainWindowViewModel { InspectorVisible: true };

            columns[SplitterColumn].Width = new GridLength(open ? SplitterWidth : 0);
            columns[DockColumn].Width = new GridLength(open ? DockWidth : 0);
        }
    }
}
