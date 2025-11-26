using Avalonia.Controls.ApplicationLifetimes;
using ControlR.DesktopClient.ViewModels;
using ControlR.DesktopClient.Views;

namespace ControlR.DesktopClient.Services;

public interface INavigationProvider
{
  Task NavigateTo<TViewModel>() where TViewModel : IViewModelBase;
  Task NavigateTo<TViewModel>(TViewModel viewModel) where TViewModel : IViewModelBase;
  void ShowMainWindow();
  Task ShowMainWindowAndNavigateTo<TViewModel>() where TViewModel : IViewModelBase;
  Task ShowMainWindowAndNavigateTo<TViewModel>(TViewModel viewModel) where TViewModel : IViewModelBase;
}

internal class NavigationProvider(
  IMainWindowProvider mainWindowProvider,
  IMainWindowViewModel mainWindowViewModel,
  IServiceProvider serviceProvider) : INavigationProvider
{
  private readonly IMainWindowProvider _mainWindowProvider = mainWindowProvider;
  private readonly IMainWindowViewModel _mainWindowViewModel = mainWindowViewModel;
  private readonly IServiceProvider _serviceProvider = serviceProvider;

  public void ShowMainWindow()
  {
    _mainWindowProvider.MainWindow.Show();
  }

  public async Task ShowMainWindowAndNavigateTo<TViewModel>() where TViewModel : IViewModelBase
  {
    ShowMainWindow();
    await NavigateTo<TViewModel>();
  }

  public async Task ShowMainWindowAndNavigateTo<TViewModel>(TViewModel viewModel) where TViewModel : IViewModelBase
  {
    ShowMainWindow();
    await NavigateTo(viewModel);
  }

  public async Task NavigateTo<TViewModel>() where TViewModel : IViewModelBase
  {
    _mainWindowViewModel.CurrentViewModel = _serviceProvider.GetRequiredService<TViewModel>();
    await _mainWindowViewModel.CurrentViewModel.Initialize();
  }

  public async Task NavigateTo<TViewModel>(TViewModel viewModel)
    where TViewModel : IViewModelBase
  {
    _mainWindowViewModel.CurrentViewModel = viewModel;
    await _mainWindowViewModel.CurrentViewModel.Initialize();
  }
}