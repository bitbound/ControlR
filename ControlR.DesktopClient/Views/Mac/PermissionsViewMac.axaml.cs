using Avalonia.Controls;
using Avalonia.Interactivity;
using ControlR.DesktopClient.ViewModels.Mac;

namespace ControlR.DesktopClient.Views.Mac;

public partial class PermissionsViewMac : UserControl
{
  public PermissionsViewMac()
  {
    InitializeComponent();
  }

  public PermissionsViewMac(IPermissionsViewModelMac viewModel)
  {
    DataContext = viewModel;
    InitializeComponent();
    App.MainWindow.Activated += MainWindow_Activated;
    Unloaded += ViewUnloaded;
  }

  private void MainWindow_Activated(object? sender, EventArgs e)
  {
    if (DataContext is not IPermissionsViewModelMac viewModel)
      return;

    viewModel.SetPermissionValues();
  }

  private void ViewUnloaded(object? sender, RoutedEventArgs e)
  {
    App.MainWindow.Activated -= MainWindow_Activated;
  }
}