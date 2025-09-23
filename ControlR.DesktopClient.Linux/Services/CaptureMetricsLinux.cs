using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Linux.Services;

public class CaptureMetricsLinux(IServiceProvider serviceProvider) : CaptureMetricsBase(serviceProvider), ICaptureMetrics
{
  
}
