using Microsoft.Extensions.Hosting;
using ControlR.Libraries.DevicesCommon.Extensions;
using ControlR.Libraries.NativeInterop.Unix;
using ControlR.Libraries.NativeInterop.Unix.Linux;
using Microsoft.Extensions.DependencyInjection;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Linux.Services;
using Microsoft.Extensions.Logging;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.DesktopClient.Linux.XdgPortal;
using ControlR.DesktopClient.Common.Services;

namespace ControlR.DesktopClient.Linux;

public static class HostAppBuilderExtensions
{
  public static IHostApplicationBuilder AddLinuxDesktopServices(
    this IHostApplicationBuilder builder,
    string appDataFolder)
  {

    var desktopEnvironment = DesktopEnvironmentDetector.Instance.GetDesktopEnvironment();
    var logger = new SerilogLogger<IHostApplicationBuilder>();

    // Register services based on detected desktop environment
    switch (desktopEnvironment)
    {
      case DesktopEnvironmentType.Wayland:
        logger.LogInformation("Detected Wayland desktop environment.");
        builder.Services
          .AddSingleton<IXdgDesktopPortalFactory, XdgDesktopPortalFactory>()
          .AddSingleton(services => services.GetRequiredService<IXdgDesktopPortalFactory>().GetOrCreateDefault())
          .AddSingleton<IDisplayManager, DisplayManagerWayland>()
          .AddSingleton<IScreenGrabberFactory, ScreenGrabberFactory<ScreenGrabberWayland>>()
          .AddSingleton(services => services.GetRequiredService<IScreenGrabberFactory>().GetOrCreateDefault())
          .AddSingleton<IInputSimulator, InputSimulatorWayland>()
          .AddSingleton<IWaylandPermissionProvider, WaylandPermissionProvider>()
          .AddSingleton<IPipeWireStreamFactory, PipeWireStreamFactory>();
        break;
      case DesktopEnvironmentType.X11:
        logger.LogInformation("Detected X11 desktop environment.");
        builder.Services
          .AddSingleton<IDisplayManager, DisplayManagerX11>()
          .AddSingleton<IScreenGrabberFactory, ScreenGrabberFactory<ScreenGrabberX11>>()
          .AddSingleton(services => services.GetRequiredService<IScreenGrabberFactory>().GetOrCreateDefault())
          .AddSingleton<IInputSimulator, InputSimulatorX11>()
          .AddHostedService<CursorWatcherX11>();
        break;
      default:
        logger.LogError("Could not detect desktop environment.");
        throw new NotSupportedException("Unsupported desktop environment detected.");
    }

    // Common services
    builder.Services.AddSingleton<IDesktopEnvironmentDetector, DesktopEnvironmentDetector>();
    builder.Services.AddSingleton<IFileSystemUnix, FileSystemUnix>();
    builder.Services.AddSingleton<ICaptureMetrics, CaptureMetricsLinux>();

    builder.BootstrapSerilog(
      logFilePath: PathConstants.GetLogsPath(appDataFolder),
      logRetention: TimeSpan.FromDays(7));

    return builder;
  }
}
