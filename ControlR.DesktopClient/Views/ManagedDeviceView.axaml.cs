using Avalonia.Controls;
using Avalonia.Interactivity;
using ControlR.DesktopClient.ViewModels;

namespace ControlR.DesktopClient.Views;

public partial class ManagedDeviceView : UserControl
{
  public ManagedDeviceView()
  {
    InitializeComponent();
  }

  public ManagedDeviceView(IManagedDeviceViewModel viewModel)
  {
    DataContext = viewModel;
    InitializeComponent();
    App.MainWindow.Activated += MainWindow_Activated;
    Unloaded += ManagedDeviceView_Unloaded;
  }

  private void ManagedDeviceView_Unloaded(object? sender, RoutedEventArgs e)
  {
    App.MainWindow.Activated -= MainWindow_Activated;
  }

  private void MainWindow_Activated(object? sender, EventArgs e)
  {
    if (DataContext is not IManagedDeviceViewModel viewModel)
      return;

    viewModel.SetPermissionValues();
  }
}
