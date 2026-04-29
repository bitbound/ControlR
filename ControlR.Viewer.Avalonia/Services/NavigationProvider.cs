using Microsoft.Extensions.DependencyInjection;
using ControlR.Viewer.Avalonia.Services.Navigation;

namespace ControlR.Viewer.Avalonia.Services;

/// <summary>
/// A service for programmatic navigation within a viewer instance.  This service is mostly intended
/// for internal use.  For public-facing navigation, use the <see cref="INavigator"/> service, 
/// which provides a more user-friendly API.
/// </summary>
public interface INavigationProvider
{
  /// <summary>
  /// Raised when navigation to a new view model occurs.
  /// </summary>
  event Action<IViewModelBase?>? NavigationOccurred;

  /// <summary>
  /// The currently active page.
  /// </summary>
  ViewerPage ActivePage { get; }

  /// <summary>
  /// Clear the currently active view model.
  /// </summary>
  Task Clear();
  /// <summary>
  /// Navigate to a view model by type, resolving it from DI.
  /// </summary>
  Task NavigateTo<TViewModel>(ViewerPage page) where TViewModel : class, IViewModelBase;
  /// <summary>
  /// Navigate to a specific view model instance.
  /// </summary>
  Task NavigateTo<TViewModel>(TViewModel viewModel, ViewerPage page) where TViewModel : IViewModelBase;
}

internal class NavigationProvider(
  IServiceProvider serviceProvider,
  ILogger<NavigationProvider> logger) : INavigationProvider
{
  private readonly ILogger<NavigationProvider> _logger = logger;
  private readonly IServiceProvider _serviceProvider = serviceProvider;

  private ViewerPage _activePage;

  public event Action<IViewModelBase?>? NavigationOccurred;

  public ViewerPage ActivePage => _activePage;

  public Task Clear()
  {
    SetActivePage(ViewerPage.None);
    NavigationOccurred?.Invoke(null);
    return Task.CompletedTask;
  }

  public async Task NavigateTo<TViewModel>(ViewerPage page) where TViewModel : class, IViewModelBase
  {
    try
    {
      var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
      await NavigateTo(viewModel, page);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to navigate to {ViewModelType}", typeof(TViewModel).Name);
      throw;
    }
  }

  public async Task NavigateTo<TViewModel>(TViewModel viewModel, ViewerPage page) where TViewModel : IViewModelBase
  {
    try
    {
      // Initialize the view model
      await viewModel.Initialize();
      
      // Notify navigation occurred with the view model
      SetActivePage(page);
      NavigationOccurred?.Invoke(viewModel);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to navigate to view model {ViewModelType}", typeof(TViewModel).Name);
      throw;
    }
  }

  private void SetActivePage(ViewerPage page)
  {
    _activePage = page;
  }
}
