using System;
using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Mac.Services;

public class CaptureMetricsMac(
  TimeProvider timeProvider,
  IMessenger messenger,
  ISystemEnvironment systemEnvironment,
  IScreenGrabber screenGrabber,
  IProcessManager processManager,
  ILogger<CaptureMetricsBase> logger) : CaptureMetricsBase(timeProvider, messenger, systemEnvironment, screenGrabber, processManager, logger), ICaptureMetrics
{
  
}
