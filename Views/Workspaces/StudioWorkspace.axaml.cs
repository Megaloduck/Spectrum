using Avalonia.Controls;
using Avalonia.Input;
using Spectrum.ViewModels;

namespace Spectrum.Views.Workspaces
{
    public partial class StudioWorkspace : DockedWorkspaceView
    {
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
    }
}