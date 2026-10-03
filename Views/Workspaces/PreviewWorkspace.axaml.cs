using Avalonia.Controls;

namespace Spectrum.Views.Workspaces
{
    public partial class PreviewWorkspace : DockedWorkspaceView
    {
        public PreviewWorkspace() => InitializeComponent();

        protected override Grid RootGrid => WorkspaceRoot;
    }
}
