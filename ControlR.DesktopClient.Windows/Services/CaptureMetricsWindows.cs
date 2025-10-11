using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.NativeInterop.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.DesktopClient.Windows.Services;


internal sealed class CaptureMetricsWindows(IServiceProvider serviceProvider) 
  : CaptureMetricsBase(serviceProvider), ICaptureMetrics
{

  private readonly IWin32Interop _win32Interop = serviceProvider.GetRequiredService<IWin32Interop>();
  protected override Dictionary<string, string> GetExtraData()
  {
    var extraData = base.GetExtraData();

    _ = _win32Interop.GetCurrentThreadDesktopName(out var threadDesktopName);
    _ = _win32Interop.GetInputDesktopName(out var inputDesktopName);

    extraData.Add("Thread ID", $"{SystemEnvironment.CurrentThreadId}");
    extraData.Add("Thread Desktop Name", $"{threadDesktopName}");
    extraData.Add("Input Desktop Name", $"{inputDesktopName}");

    return extraData;
  }
}
