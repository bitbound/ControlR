using Avalonia.Controls;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Services;
using ControlR.DesktopClient.ViewModels;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Views;
public partial class MainWindow : Window
{
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

  protected override void OnClosing(WindowClosingEventArgs e)
  {
    e.Cancel = true;
    Hide();
    base.OnClosing(e);
  }
}