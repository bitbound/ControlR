using ControlR.DesktopClient.Common.ServiceInterfaces;

namespace ControlR.DesktopClient.Linux.Services;

public class CaptureMetricsLinux : ICaptureMetrics
{
  public Dictionary<string, string> GetExtraMetricsData()
  {
    return [];
  }
}
