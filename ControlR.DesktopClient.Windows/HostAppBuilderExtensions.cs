using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Windows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ControlR.Libraries.DevicesCommon.Extensions;
using ControlR.Libraries.NativeInterop.Windows;

namespace ControlR.DesktopClient.Windows;
public static class HostAppBuilderExtensions
{
  public static IHostApplicationBuilder AddWindowsDesktopServices(
    this IHostApplicationBuilder builder,
    string appDataFolder)
  {
    builder.Services
      .AddSingleton<IWin32Interop, Win32Interop>()
      .AddSingleton<InputSimulatorWindows>()
      .AddSingleton<IInputSimulator>(services => services.GetRequiredService<InputSimulatorWindows>())
      .AddSingleton<ICaptureMetrics, CaptureMetricsWindows>()
      .AddSingleton<IClipboardManager, ClipboardManagerWindows>()
      .AddSingleton<IDisplayManager, DisplayManagerWindows>()
      .AddSingleton<IScreenGrabber, ScreenGrabberWindows>()
      .AddSingleton<IDxOutputDuplicator, DxOutputDuplicator>()
      .AddHostedService<SystemEventHandler>()
      .AddHostedService<InputDesktopReporter>()
      .AddHostedService(x => x.GetRequiredService<ICaptureMetrics>())
      .AddHostedService(x => x.GetRequiredService<InputSimulatorWindows>())
      .AddHostedService<CursorWatcherWindows>();

    builder.BootstrapSerilog(
      logFilePath: PathConstants.GetLogsPath(appDataFolder),
      logRetention: TimeSpan.FromDays(7));

    return builder;
  }
}
