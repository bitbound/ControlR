using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.DesktopClient.Services;

namespace ControlR.DesktopClient.ViewModels;

public interface IMainWindowViewModel : IViewModelBase
{
  IViewModelBase CurrentViewModel { get; set; }

  void HandleMainWindowClosed();
}

public partial class MainWindowViewModel(IMainWindowProvider mainWindowProvider) : ViewModelBase, IMainWindowViewModel
{
  private readonly IMainWindowProvider _mainWindowProvider = mainWindowProvider;

  [ObservableProperty]
  private IViewModelBase? _currentViewModel;

  public void HandleMainWindowClosed()
  {
    _mainWindowProvider.HandleMainWindowClosed();
  }
}
