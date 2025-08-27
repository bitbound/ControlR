using System.Drawing;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;

namespace ControlR.DesktopClient.Linux.Services;

internal class ScreenGrabberLinux() : IScreenGrabber
{
  public CaptureResult Capture(DisplayInfo targetDisplay, bool captureCursor = true, bool tryUseGpuAcceleration = true, int gpuCaptureTimeout = 50, bool allowFallbackToCpu = true)
  {
    throw new NotImplementedException();
  }

  public CaptureResult Capture(bool captureCursor = true)
  {
    throw new NotImplementedException();
  }

  public IEnumerable<DisplayInfo> GetDisplays()
  {
    throw new NotImplementedException();
  }

  public Rectangle GetVirtualScreenBounds()
  {
    throw new NotImplementedException();
  }
}