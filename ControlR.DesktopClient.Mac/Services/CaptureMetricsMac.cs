using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Mac.Services;

public class CaptureMetricsMac(IServiceProvider serviceProvider) : CaptureMetricsBase(serviceProvider), ICaptureMetrics
{
  
}
