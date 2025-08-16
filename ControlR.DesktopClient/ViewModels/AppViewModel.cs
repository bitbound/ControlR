using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using ControlR.DesktopClient.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Input;

namespace ControlR.DesktopClient.ViewModels;

public interface IAppViewModel
{
  ICommand ExitApplicationCommand { get; }
  ICommand ShowWindowCommand { get; }
}

public class AppViewModel : ViewModelBase, IAppViewModel
{
  private readonly IClassicDesktopStyleApplicationLifetime _desktop;
  private readonly IServiceProvider _serviceProvider;

  public AppViewModel(IClassicDesktopStyleApplicationLifetime desktop, IServiceProvider serviceProvider)
  {
    _desktop = desktop;
    _serviceProvider = serviceProvider;

    ShowWindowCommand = new RelayCommand(ShowMainWindow);
    ExitApplicationCommand = new RelayCommand(() => _desktop.Shutdown());
  }

  private void ShowMainWindow()
  {
    _desktop.MainWindow ??= _serviceProvider.GetRequiredService<MainWindow>();
    _desktop.MainWindow.Show();
  }

  public ICommand ShowWindowCommand { get; }
  public ICommand ExitApplicationCommand { get; }
}