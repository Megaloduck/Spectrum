using Avalonia.Controls;

namespace Spectrum.Views.Workspaces
{
    public partial class AnalyzeWorkspace : DockedWorkspaceView
    {
        public AnalyzeWorkspace() => InitializeComponent();

        protected override Grid RootGrid => WorkspaceRoot;
    }
}
