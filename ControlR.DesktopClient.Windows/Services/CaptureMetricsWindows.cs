using System.Text.Json;
using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Windows.Services;


internal sealed class CaptureMetricsWindows(
  TimeProvider timeProvider,
  IMessenger messenger,
  IWin32Interop win32Interop,
  ISystemEnvironment systemEnvironment,
  IScreenGrabber screenGrabber,
  IProcessManager processManager,
  ILogger<CaptureMetricsWindows> logger) 
  : CaptureMetricsBase(timeProvider, messenger, systemEnvironment, screenGrabber, processManager, logger), ICaptureMetrics
{

  private readonly IWin32Interop _win32Interop = win32Interop;
  private readonly JsonSerializerOptions _jsonSerializerOptions = new()
  {
    WriteIndented = true,
  };

  protected override Dictionary<string, string> GetExtraData()
  {
    var extraData = base.GetExtraData();

    _ = _win32Interop.GetCurrentThreadDesktopName(out var threadDesktopName);
    _ = _win32Interop.GetInputDesktopName(out var inputDesktopName);
    var screenBounds = _screenGrabber.GetVirtualScreenBounds();

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
