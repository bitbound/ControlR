using Microsoft.Extensions.Hosting;
using ControlR.Libraries.DevicesCommon.Extensions;
using ControlR.Libraries.NativeInterop.Unix;
using Microsoft.Extensions.DependencyInjection;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Linux.Services;

namespace ControlR.DesktopClient.Linux;

public static class HostAppBuilderExtensions
{
  public static IHostApplicationBuilder AddLinuxDesktopServices(
    this IHostApplicationBuilder builder,
    string appDataFolder)
  {
    builder.Services
      .AddSingleton<IFileSystemUnix, FileSystemUnix>()
      .AddSingleton<IDisplayManager, DisplayManagerX11>()
      .AddSingleton<IScreenGrabber, ScreenGrabberX11>()
      //.AddSingleton<IClipboardManager, ClipboardManagerX11>()
      //.AddSingleton<IClipboardManager, ClipboardManagerGtk>()
      .AddSingleton<ICaptureMetrics, CaptureMetricsLinux>()
      .AddSingleton<IInputSimulator, InputSimulatorX11>()
      .AddHostedService(x => x.GetRequiredService<ICaptureMetrics>())
      .AddHostedService<CursorWatcherX11>();
      
    builder.BootstrapSerilog(
      logFilePath: PathConstants.GetLogsPath(appDataFolder),
      logRetention: TimeSpan.FromDays(7));

    return builder;
  }
}
