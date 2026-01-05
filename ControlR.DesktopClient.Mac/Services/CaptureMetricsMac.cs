using ControlR.DesktopClient.Common.ServiceInterfaces;

namespace ControlR.DesktopClient.Mac.Services;

public class CaptureMetricsMac : ICaptureMetrics
{
  public Dictionary<string, string> GetExtraMetricsData()
  {
    return [];
  }
}
