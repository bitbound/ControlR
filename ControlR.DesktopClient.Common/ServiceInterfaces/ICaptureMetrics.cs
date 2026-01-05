using Microsoft.Extensions.Hosting;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

/// <summary>
/// Provides metrics for video capture, such as FPS and bandwidth usage.
/// </summary>
public interface ICaptureMetrics
{
  /// <summary>
  /// Retrieves a collection of additional metrics data as key-value pairs.
  /// </summary>
  Dictionary<string, string> GetExtraMetricsData();
}
