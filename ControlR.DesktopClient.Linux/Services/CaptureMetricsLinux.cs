using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;

namespace ControlR.DesktopClient.Linux.Services;

public class CaptureMetricsLinux(IServiceProvider serviceProvider) 
  : CaptureMetricsBase(serviceProvider), ICaptureMetrics
{
  
}
