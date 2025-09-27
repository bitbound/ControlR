using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;

namespace ControlR.DesktopClient.Mac.Services;

public class CaptureMetricsMac(IServiceProvider serviceProvider) : CaptureMetricsBase(serviceProvider), ICaptureMetrics
{
  
}
