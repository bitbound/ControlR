using Avalonia.Controls;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Linux.XdgPortal;
using ControlR.DesktopClient.Linux.Services;
using ControlR.DesktopClient.ViewModels;
using ControlR.DesktopClient.ViewModels.Linux;
using ControlR.Libraries.NativeInterop.Linux;
using ControlR.Libraries.NativeInterop.Unix;
using ControlR.Libraries.Serilog;
using ControlR.Libraries.Shared.Services.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Linux;

public static class ServiceRegistrationExtensions
{
  public static IServiceCollection AddDesktopAppPlatformServices(this IServiceCollection services)
  {
    services
      .AddSharedPlatformServices()
      .AddSingleton<INavigationItemProvider, LinuxNavigationItemProvider>()
      .AddSingleton<IRemoteControlHostBuilderFactory, LinuxRemoteControlHostBuilderFactory>();

    return GetDesktopEnvironmentDetector().GetDesktopEnvironment() switch
    {
      DesktopEnvironmentType.Wayland => services
        .AddSingleton<IPermissionsViewModelWayland, PermissionsViewModelWayland>()
        .AddHostedService<RemoteControlPermissionMonitorWayland>(),
      DesktopEnvironmentType.X11 => services
        .AddSingleton<IPermissionsViewModel, PermissionsViewModel>(),
      _ => throw new NotSupportedException("Unsupported desktop environment detected.")
    };
  }

  public static IHostApplicationBuilder AddRemoteControlPlatformServices(this IHostApplicationBuilder builder)
  {
    builder.Services.AddSharedPlatformServices();
    AddInputSimulator(builder.Services);
    AddRemoteControlHostedServices(builder.Services);

    return builder;
  }

  public static IServiceCollection AddSharedPlatformServices(this IServiceCollection services)
  {
    var desktopEnvironment = GetDesktopEnvironmentDetector().GetDesktopEnvironment();
    var logger = new SerilogLogger<IServiceCollection>();

    switch (desktopEnvironment)
    {
      case DesktopEnvironmentType.Wayland:
        logger.LogInformation("Detected Wayland desktop environment.");
        services
          .AddSingleton<IXdgDesktopPortalFactory, XdgDesktopPortalFactory>()
          .AddSingleton(provider => provider.GetRequiredService<IXdgDesktopPortalFactory>().GetOrCreateDefault())
          .AddSingleton<DisplayManagerWayland>()
          .AddSingleton<IDisplayManager>(provider => provider.GetRequiredService<DisplayManagerWayland>())
          .AddSingleton<IDisplayManagerWayland>(provider => provider.GetRequiredService<DisplayManagerWayland>())
          .AddSingleton<IScreenGrabberFactory, ScreenGrabberFactory<ScreenGrabberWayland>>()
          .AddSingleton(provider => provider.GetRequiredService<IScreenGrabberFactory>().GetOrCreateDefault())
          .AddSingleton<IClipboardManager, ClipboardManagerGtk>()
          .AddSingleton<IWaylandPermissionProvider, WaylandPermissionProvider>()
          .AddSingleton<IPipeWireStreamFactory, PipeWireStreamFactory>();
        break;
      case DesktopEnvironmentType.X11:
        logger.LogInformation("Detected X11 desktop environment.");
        services
          .AddSingleton<IDisplayManager, DisplayManagerX11>()
          .AddSingleton<IScreenGrabberFactory, ScreenGrabberFactory<ScreenGrabberX11>>()
          .AddSingleton(provider => provider.GetRequiredService<IScreenGrabberFactory>().GetOrCreateDefault())
          .AddSingleton<IClipboardManager, ClipboardManagerX11>();
        break;
      default:
        logger.LogError("Could not detect desktop environment.");
        throw new NotSupportedException("Unsupported desktop environment detected.");
    }

    return services
      .AddSingleton<IFileAccessPermissions, FileAccessPermissions>()
      .AddSingleton<IDesktopEnvironmentDetector, DesktopEnvironmentDetector>()
      .AddSingleton<IFileSystemUnix, FileSystemUnix>()
      .AddSingleton<ICaptureMetrics, CaptureMetricsLinux>()
      .AddSingleton<IDesktopClientPermissionService, DesktopClientPermissionServiceLinux>();
  }

  private static IServiceCollection AddInputSimulator(IServiceCollection services)
  {
    return GetDesktopEnvironmentDetector().GetDesktopEnvironment() switch
    {
      DesktopEnvironmentType.Wayland => services
        .AddSingleton<IInputSimulator, InputSimulatorWayland>(),
      DesktopEnvironmentType.X11 => services
        .AddSingleton<IInputSimulator, InputSimulatorX11>(),
      _ => throw new NotSupportedException("Unsupported desktop environment detected.")
    };
  }

  private static IServiceCollection AddRemoteControlHostedServices(IServiceCollection services)
  {
    return GetDesktopEnvironmentDetector().GetDesktopEnvironment() switch
    {
      DesktopEnvironmentType.Wayland => services.AddHostedService<WaylandDisplaySettingsWatcher>(),
      DesktopEnvironmentType.X11 => services.AddHostedService<CursorWatcherX11>(),
      _ => throw new NotSupportedException("Unsupported desktop environment detected.")
    };
  }

  private static DesktopEnvironmentDetector GetDesktopEnvironmentDetector()
  {
    return new DesktopEnvironmentDetector(
      new SerilogLogger<DesktopEnvironmentDetector>());
  }
}