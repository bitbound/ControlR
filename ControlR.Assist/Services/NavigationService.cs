using ControlR.Assist.ViewModels;

namespace ControlR.Assist.Services;

public interface INavigationService
{
  event Action<ViewModelBase>? NavigateRequested;

  void NavigateTo<TViewModel>()
    where TViewModel : ViewModelBase;
}

public sealed class NavigationService(IServiceProvider serviceProvider) : INavigationService
{
  private readonly IServiceProvider _serviceProvider = serviceProvider;

  public event Action<ViewModelBase>? NavigateRequested;

  public void NavigateTo<TViewModel>()
    where TViewModel : ViewModelBase
  {
    NavigateRequested?.Invoke(_serviceProvider.GetRequiredService<TViewModel>());
  }
}
