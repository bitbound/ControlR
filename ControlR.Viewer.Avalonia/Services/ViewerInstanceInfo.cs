using ControlR.ApiClient;
using ControlR.Viewer.Avalonia.Services.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Viewer.Avalonia.Services;


/// <summary>
/// Information about a registered viewer instance.
/// </summary>
public record ViewerInstanceInfo(Guid InstanceId, ControlrViewer Viewer, IServiceProvider ServiceProvider)
{
  public IControlrAuthSession GetAuthSession() => ServiceProvider.GetRequiredService<IControlrAuthSession>();
  public IHubConnection<IViewerHub> GetHubConnection() => ServiceProvider.GetRequiredService<IHubConnection<IViewerHub>>();
  public IControlrApi GetControlrApi() => ServiceProvider.GetRequiredService<IControlrApi>();
  public INavigator GetNavigator() => ServiceProvider.GetRequiredService<INavigator>();
}