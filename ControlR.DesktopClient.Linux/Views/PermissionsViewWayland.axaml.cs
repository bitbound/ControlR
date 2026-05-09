using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using ControlR.DesktopClient.ViewModels.Linux;
using ControlR.Libraries.Shared.Extensions;

namespace ControlR.DesktopClient.Views.Linux;

public partial class PermissionsViewWayland : UserControl
{
  private Window? _mainWindow;

  public PermissionsViewWayland()
  {
    InitializeComponent();
  }
  public PermissionsViewWayland(IPermissionsViewModelWayland viewModel)
  {
    DataContext = viewModel;
    InitializeComponent();
    _mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    if (_mainWindow is null)
    {
      return;
    }

    _mainWindow.Activated += MainWindowActivated;
    Unloaded += ViewUnloaded;
  }

  protected override void OnLoaded(RoutedEventArgs e)
  {
    base.OnLoaded(e);

    if (DataContext is IPermissionsViewModelWayland viewModel)
    {
      viewModel.SetPermissionValues().Forget();
    }
  }

  private void MainWindowActivated(object? sender, EventArgs e)
  {
    if (DataContext is not IPermissionsViewModelWayland viewModel)
    {
      return;
    }

    viewModel.SetPermissionValues().Forget();
  }

  private void ViewUnloaded(object? sender, RoutedEventArgs e)
  {
    if (_mainWindow is not null)
    {
      _mainWindow.Activated -= MainWindowActivated;
    }
  }
}
