using System.Text.Json;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.NativeInterop.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.DesktopClient.Windows.Services;


internal sealed class CaptureMetricsWindows(IServiceProvider serviceProvider) 
  : CaptureMetricsBase(serviceProvider), ICaptureMetrics
{

  private readonly IWin32Interop _win32Interop = serviceProvider.GetRequiredService<IWin32Interop>();
  private readonly IDisplayManager _displayManager = serviceProvider.GetRequiredService<IDisplayManager>();
  private readonly JsonSerializerOptions _jsonSerializerOptions = new()
  {
    WriteIndented = true,
  };

  protected override Dictionary<string, string> GetExtraData()
  {
    var extraData = base.GetExtraData();

    _ = _win32Interop.GetCurrentThreadDesktopName(out var threadDesktopName);
    _ = _win32Interop.GetInputDesktopName(out var inputDesktopName);
    var screenBounds = _displayManager.GetVirtualScreenBounds();

    extraData.Add("Thread ID", $"{_systemEnvironment.CurrentThreadId}");
    extraData.Add("Thread Desktop Name", $"{threadDesktopName}");
    extraData.Add("Input Desktop Name", $"{inputDesktopName}");
    extraData.Add("Screen Bounds", JsonSerializer.Serialize(screenBounds, _jsonSerializerOptions));

    if (_processManager.GetCurrentProcess().SessionId == 0)
    {
      var windowInfos = _win32Interop.GetVisibleWindows();
      foreach (var item in windowInfos.Index())
      {
        extraData.Add($"Window Data ({item.Index})", item.Item.ToString());
      }

      var desktopNames = _win32Interop.GetDesktopNames();
      foreach (var item in desktopNames.Index())
      {
        extraData.Add($"Desktop ({item.Index})", item.Item);
      }

    }

    return extraData;
  }

}
