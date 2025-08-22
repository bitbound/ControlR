using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.DesktopClient.Common.Services;
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
      .AddSingleton<IInputSimulator, InputSimulatorWindows>()
      .AddSingleton<ICaptureMetrics, CaptureMetricsWindows>()
      .AddSingleton<IClipboardManager, ClipboardManagerWindows>()
      .AddSingleton<IScreenGrabber, ScreenGrabberWindows>()
      .AddSingleton<IDxOutputGenerator, DxOutputGenerator>()
      .AddHostedService<SystemEventHandler>()
      .AddHostedService<InputDesktopReporter>()
      .AddHostedService<CursorWatcher>();

    builder.BootstrapSerilog(
      logFilePath: PathConstants.GetLogsPath(appDataFolder),
      logRetention: TimeSpan.FromDays(7));

    return builder;
  }
}
