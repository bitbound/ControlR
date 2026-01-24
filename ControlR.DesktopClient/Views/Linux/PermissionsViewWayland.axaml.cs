using Avalonia.Controls;
using Avalonia.Interactivity;
using ControlR.DesktopClient.ViewModels.Linux;

namespace ControlR.DesktopClient.Views.Linux;

public partial class PermissionsViewWayland : UserControl
{
  public PermissionsViewWayland()
  {
    InitializeComponent();
  }

  public PermissionsViewWayland(IPermissionsViewModelWayland viewModel)
  {
    DataContext = viewModel;
    InitializeComponent();
    App.MainWindow.Activated += MainWindow_Activated;
    Unloaded += ViewUnloaded;
  }

  private void MainWindow_Activated(object? sender, EventArgs e)
  {
    if (DataContext is not IPermissionsViewModelWayland viewModel)
      return;

    viewModel.SetPermissionValues();
  }

  private void ViewUnloaded(object? sender, RoutedEventArgs e)
  {
    App.MainWindow.Activated -= MainWindow_Activated;
  }
}