using Microsoft.Extensions.Hosting;
using ControlR.Libraries.DevicesCommon.Extensions;
using ControlR.Libraries.NativeInterop.Unix;
using ControlR.Libraries.NativeInterop.Unix.Linux;
using Microsoft.Extensions.DependencyInjection;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Linux.Services;
using Microsoft.Extensions.Logging;
using ControlR.Libraries.DevicesCommon.Services;

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
          .AddSingleton<IWaylandPortalAccessor, WaylandPortalAccessor>()
          .AddSingleton<IDisplayManager, DisplayManagerWayland>()
          .AddSingleton<IScreenGrabber, ScreenGrabberWayland>()
          .AddSingleton<IInputSimulator, InputSimulatorWayland>()
          .AddSingleton<IWaylandPermissionProvider, WaylandPermissionProvider>()
          .AddSingleton<IPipeWireStreamFactory, PipeWireStreamFactory>();
        break;
      case DesktopEnvironmentType.X11:
        logger.LogInformation("Detected X11 desktop environment.");
        builder.Services
          .AddSingleton<IDisplayManager, DisplayManagerX11>()
          .AddSingleton<IScreenGrabber, ScreenGrabberX11>()
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
    builder.Services
      .AddSingleton<ICaptureMetrics, CaptureMetricsLinux>()
      .AddHostedService(x => x.GetRequiredService<ICaptureMetrics>());

    builder.BootstrapSerilog(
      logFilePath: PathConstants.GetLogsPath(appDataFolder),
      logRetention: TimeSpan.FromDays(7));

    return builder;
  }
}
