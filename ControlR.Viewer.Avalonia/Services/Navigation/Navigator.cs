using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Viewer.Avalonia.Services.Navigation;

/// <summary>
/// A public-facing service for navigating between different pages within a viewer instance.
/// </summary>
public interface INavigator
{
  Task<Result> NavigateTo(ViewerPage page);
}

internal class Navigator(
  INavigationProvider navigationProvider,
  ILogger<Navigator> logger) : INavigator
{
  private readonly ILogger<Navigator> _logger = logger;
  private readonly INavigationProvider _navigationProvider = navigationProvider;

  public async Task<Result> NavigateTo(ViewerPage page)
  {
    try
    {
      switch (page)
      {
        case ViewerPage.None:
          await _navigationProvider.Clear();
          break;
        case ViewerPage.RemoteControl:
          await _navigationProvider.NavigateTo<IRemoteControlViewModel>();
          break;
        case ViewerPage.Terminal:
          await _navigationProvider.NavigateTo<ITerminalViewModel>();
          break;
        default:
          return Result.Fail($"Unsupported page: {page}.");
      }
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to navigate to page '{Page}'.", page);
      return Result.Fail($"Failed to navigate to page '{page}': {ex.Message}");
    }
  }
}