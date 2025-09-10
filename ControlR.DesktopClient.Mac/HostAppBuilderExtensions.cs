using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Mac.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ControlR.Libraries.DevicesCommon.Extensions;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.NativeInterop.Unix.MacOs;
using ControlR.Libraries.NativeInterop.Unix;

namespace ControlR.DesktopClient.Mac;

public static class HostAppBuilderExtensions
{
  public static IHostApplicationBuilder AddMacDesktopServices(
    this IHostApplicationBuilder builder,
    string appDataFolder)
  {
    builder.Services
      .AddSingleton<IMacInterop, MacInterop>()
      .AddSingleton<IScreenGrabber, ScreenGrabberMac>()
      .AddSingleton<IClipboardManager, ClipboardManagerMac>()
      .AddSingleton<ICaptureMetrics, CaptureMetricsMac>()
      .AddSingleton<IInputSimulator, InputSimulatorMac>()
      .AddSingleton<IFileSystemUnix, FileSystemUnix>()
      .AddHostedService<ScreenWakerMac>();
    
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
