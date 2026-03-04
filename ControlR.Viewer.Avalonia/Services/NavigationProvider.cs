using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Viewer.Avalonia.Services;

/// <summary>
/// Interface for programmatic navigation within a viewer instance.
/// </summary>
public interface INavigationProvider
{
  /// <summary>
  /// Raised when navigation to a new view model occurs.
  /// </summary>
  event Action<IViewModelBase>? NavigationOccurred;

  /// <summary>
  /// The currently active view model type, if any.
  /// </summary>
  Type? ActiveViewModel { get; }

  /// <summary>
  /// Navigate to a view model by type, resolving it from DI.
  /// </summary>
  Task NavigateTo<TViewModel>() where TViewModel : class, IViewModelBase;

  /// <summary>
  /// Navigate to a specific view model instance.
  /// </summary>
  Task NavigateTo<TViewModel>(TViewModel viewModel) where TViewModel : IViewModelBase;
}

internal class NavigationProvider(
  IServiceProvider serviceProvider,
  ILogger<NavigationProvider> logger) : INavigationProvider
{
  private readonly ILogger<NavigationProvider> _logger = logger;
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  private Type? _activeViewModelType;

  public event Action<IViewModelBase>? NavigationOccurred;

  public Type? ActiveViewModel => _activeViewModelType;

  public async Task NavigateTo<TViewModel>() where TViewModel : class, IViewModelBase
  {
    try
    {
      var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
      await NavigateTo(viewModel);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to navigate to {ViewModelType}", typeof(TViewModel).Name);
      throw;
    }
  }

  public async Task NavigateTo<TViewModel>(TViewModel viewModel) where TViewModel : IViewModelBase
  {
    try
    {
      // Initialize the view model
      await viewModel.Initialize();
      
      // Notify navigation occurred with the view model
      SetActiveViewModelType(viewModel.GetType());
      NavigationOccurred?.Invoke(viewModel);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to navigate to view model {ViewModelType}", typeof(TViewModel).Name);
      throw;
    }
  }
  private void SetActiveViewModelType(Type? type)
  {
    _activeViewModelType = type;
  }
}
