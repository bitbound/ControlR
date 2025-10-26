using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ControlR.DesktopClient.ViewModels;

public interface IMainWindowViewModel : IViewModelBase
{
  IViewModelBase CurrentViewModel { get; set; }

  void SetMainWindowNull();
}

public partial class MainWindowViewModel(IClassicDesktopStyleApplicationLifetime desktop) : ViewModelBase, IMainWindowViewModel
{
  private readonly IClassicDesktopStyleApplicationLifetime _desktop = desktop;

  [ObservableProperty]
  private IViewModelBase? _currentViewModel;

  public void SetMainWindowNull()
  {
    _desktop.MainWindow = null;
  }
}
