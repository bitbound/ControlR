using Avalonia.Controls;
using ControlR.DesktopClient.Services;
using ControlR.DesktopClient.ViewModels;

namespace ControlR.DesktopClient.Views;

public partial class MainWindow : Window
{
  private bool _isFirstShow = true;

  public MainWindow()
  {
    InitializeComponent();
  }

  public MainWindow(
    IMainWindowViewModel viewModel,
    INavigationProvider navigationProvider)
  {
    DataContext = viewModel;
    InitializeComponent();
    navigationProvider.NavigateTo<IManagedDeviceViewModel>();
  }

  private IMainWindowViewModel ViewModel => DataContext as IMainWindowViewModel
                                            ?? throw new ArgumentNullException(nameof(ViewModel));

  public override void Show()
  {
    base.Show();
    if (!_isFirstShow)
    {
      return;
    }

    _isFirstShow = false;
    Hide();
    ShowInTaskbar = true;
    WindowState = WindowState.Normal;
  }

  protected override void OnClosing(WindowClosingEventArgs e)
  {
    e.Cancel = true;
    Hide();
    base.OnClosing(e);
  }
}