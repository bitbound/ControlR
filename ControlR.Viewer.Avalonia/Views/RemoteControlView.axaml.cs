using Avalonia.Controls;

namespace ControlR.Viewer.Avalonia.Views;

public partial class RemoteControlView : UserControl
{
    public RemoteControlView()
    {
        InitializeComponent();
    }

    public RemoteControlView(RemoteControlViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
