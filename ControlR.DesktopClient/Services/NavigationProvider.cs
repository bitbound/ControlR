using ControlR.DesktopClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.DesktopClient.Services;

public interface INavigationProvider
{
  Task NavigateTo<TViewModel>() where TViewModel : IViewModelBase;
  Task NavigateTo<TViewModel>(TViewModel viewModel)
    where TViewModel : IViewModelBase;
}

internal class NavigationProvider(
  IMainWindowViewModel mainWindowViewModel,
  IServiceProvider serviceProvider) : INavigationProvider
{
  private readonly IMainWindowViewModel _mainWindowViewModel = mainWindowViewModel;
  private readonly IServiceProvider _serviceProvider = serviceProvider;

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