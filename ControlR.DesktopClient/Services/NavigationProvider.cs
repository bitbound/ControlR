using ControlR.DesktopClient.ViewModels;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Services;

public interface INavigationProvider
{
  /// <summary>
  ///   Raised when navigation to a new view model occurs.
  /// </summary>
  event Action<Type?>? NavigationOccurred;

  /// <summary>
  /// The currently active view model type, if any.
  /// </summary>
  Type? ActiveViewModel { get; }

  Task NavigateTo<TViewModel>() where TViewModel : IViewModelBase;
  Task NavigateTo<TViewModel>(TViewModel viewModel) where TViewModel : IViewModelBase;
  void ShowMainWindow();
  Task ShowMainWindowAndNavigateTo<TViewModel>() where TViewModel : IViewModelBase;
  Task ShowMainWindowAndNavigateTo<TViewModel>(TViewModel viewModel) where TViewModel : IViewModelBase;
}

internal class NavigationProvider(
  IMainWindowProvider mainWindowProvider,
  ILogger<NavigationProvider> logger,
  IServiceProvider serviceProvider) : INavigationProvider
{
  private readonly ILogger<NavigationProvider> _logger = logger;
  private readonly IMainWindowProvider _mainWindowProvider = mainWindowProvider;
  private readonly IServiceProvider _serviceProvider = serviceProvider;

  private Type? _activeViewModelType;

  public event Action<Type?>? NavigationOccurred;

  public Type? ActiveViewModel => _activeViewModelType;

  public async Task NavigateTo<TViewModel>() where TViewModel : IViewModelBase
  {
    var mainWindowVm = _serviceProvider.GetRequiredService<IMainWindowViewModel>();
    mainWindowVm.CurrentViewModel = _serviceProvider.GetRequiredService<TViewModel>();
    await mainWindowVm.CurrentViewModel.Initialize();

    SetActiveViewModelType(typeof(TViewModel));
  }

  public async Task NavigateTo<TViewModel>(TViewModel viewModel)
    where TViewModel : IViewModelBase
  {
    var mainWindowVm = _serviceProvider.GetRequiredService<IMainWindowViewModel>();
    mainWindowVm.CurrentViewModel = viewModel;
    await mainWindowVm.CurrentViewModel.Initialize();

    SetActiveViewModelType(viewModel?.GetType() ?? typeof(TViewModel));
  }

  public void ShowMainWindow()
  {
    if (_mainWindowProvider.MainWindow.IsVisible)
    {
      _mainWindowProvider.MainWindow.Activate();
      _mainWindowProvider.MainWindow.ShowInTaskbar = true;
      return;
    }
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

  private void SetActiveViewModelType(Type? type)
  {
    _activeViewModelType = type;
    try
    {
      NavigationOccurred?.Invoke(type);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while invoking NavigationOccurred handlers.");
    }
  }
}