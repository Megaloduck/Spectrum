using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Spectrum.ViewModels;
using Spectrum.Views.Widgets;
using System;

namespace Spectrum.Views.Workspaces
{
    public partial class StudioWorkspace : DockedWorkspaceView
    {
        private SliderRow? _sliderRow;

        public StudioWorkspace() => InitializeComponent();

        protected override Grid RootGrid => WorkspaceRoot;

        /// <summary>Clicking the big base-color preview opens the spectrum picker (§2).</summary>
        private void OnBasePreviewPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm && vm.Studio.OpenColorPickerCommand.CanExecute(null))
            {
                vm.Studio.OpenColorPickerCommand.Execute(null);
                e.Handled = true;
            }
        }

        /// <summary>Feeds the five color-panel sliders into the shared SliderRow control.</summary>
        public void BindSliders(StudioWorkspaceViewModel studio)
        {
            _sliderRow?.BindTo(studio);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is MainWindowViewModel vm)
            {
                _sliderRow = this.FindControl<SliderRow>("SliderRow");
                BindSliders(vm.Studio);
            }
        }
    }
}
