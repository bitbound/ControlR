using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Mac.Services;
using ControlR.DesktopClient.Mac.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ControlR.Libraries.Serilog;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.NativeInterop.Unix;
using ControlR.DesktopClient.Common.Services;

namespace ControlR.DesktopClient.Mac;

public static class HostAppBuilderExtensions
{
  public static IHostApplicationBuilder AddMacDesktopServices(
    this IHostApplicationBuilder builder,
    string appDataFolder)
  {
    builder.Services
      .AddSingleton<IMacInterop, MacInterop>()
      .AddSingleton<IDisplayManager, DisplayManagerMac>()
      .AddSingleton<IDisplayEnumHelperMac, DisplayEnumHelperMac>()
      .AddSingleton<IScreenGrabberFactory, ScreenGrabberFactory<ScreenGrabberMac>>()
      .AddSingleton(services => services.GetRequiredService<IScreenGrabberFactory>().GetOrCreateDefault())
      .AddSingleton<IClipboardManager, ClipboardManagerMac>()
      .AddSingleton<ICaptureMetrics, CaptureMetricsMac>()
      .AddSingleton<IInputSimulator, InputSimulatorMac>()
      .AddSingleton<IFileSystemUnix, FileSystemUnix>()
      .AddHostedService<ScreenWakerMac>()
      .AddHostedService<CursorWatcherMac>();
    
    builder.BootstrapSerilog(
      logFilePath: PathConstants.GetLogsPath(appDataFolder),
      logRetention: TimeSpan.FromDays(7),
      config =>
      {
        if (SystemEnvironment.Instance.IsDebug)
        {
          config.MinimumLevel.Debug();
        }
      });

    return builder;
  }
}
