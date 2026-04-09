using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Mac.Helpers;
using ControlR.DesktopClient.Mac.Services;
using ControlR.DesktopClient.ViewModels.Mac;
using ControlR.Libraries.NativeInterop.Unix;
using ControlR.Libraries.Serilog;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Mac;

public static class ServiceRegistrationExtensions
{
  public static IServiceCollection AddDesktopAppPlatformServices(this IServiceCollection services)
  {
    return services
      .AddSharedPlatformServices()
      .AddSingleton<INavigationItemProvider, MacNavigationItemProvider>()
      .AddSingleton<IRemoteControlHostBuilderFactory, MacRemoteControlHostBuilderFactory>()
      .AddSingleton<IPermissionsViewModelMac, PermissionsViewModelMac>()
      .AddHostedService<RemoteControlPermissionMonitorMac>();
  }

  public static IHostApplicationBuilder AddRemoteControlPlatformServices(this IHostApplicationBuilder builder)
  {
    builder.Services.AddSharedPlatformServices();
    AddRemoteControlHostedServices(builder.Services);

    return builder;
  }

  public static IServiceCollection AddSharedPlatformServices(this IServiceCollection services)
  {
    return services
      .AddSingleton<IMacInterop>(provider => new MacInterop(provider.GetRequiredService<ILogger<MacInterop>>()))
      .AddSingleton<IDisplayManager, DisplayManagerMac>()
      .AddSingleton<IDisplayEnumHelperMac, DisplayEnumHelperMac>()
      .AddSingleton<IScreenGrabberFactory, ScreenGrabberFactory<ScreenGrabberMac>>()
      .AddSingleton(provider => provider.GetRequiredService<IScreenGrabberFactory>().GetOrCreateDefault())
      .AddSingleton<IClipboardManager, ClipboardManagerMac>()
      .AddSingleton<ICaptureMetrics, CaptureMetricsMac>()
      .AddSingleton<IInputSimulator, InputSimulatorMac>()
      .AddSingleton<IFileSystemUnix, FileSystemUnix>();
  }

  private static IServiceCollection AddRemoteControlHostedServices(IServiceCollection services)
  {
    return services
      .AddHostedService<ScreenWakerMac>()
      .AddHostedService<CursorWatcherMac>();
  }
}