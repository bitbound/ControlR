using Microsoft.Extensions.Hosting;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

/// <summary>
/// Provides metrics for video capture, such as FPS and bandwidth usage.
/// </summary>
public interface ICaptureMetrics : IHostedService, IDisposable
{
  /// <summary>
  /// Gets the current frames per second.
  /// </summary>
  double Fps { get; }

  /// <summary>
  /// Gets a value indicating whether the GPU is being used for video encoding.
  /// </summary>
  bool IsUsingGpu { get; }

  /// <summary>
  /// Gets the current data transfer rate in megabits per second.
  /// </summary>
  double Mbps { get; }

  /// <summary>
  /// Records that a block of data has been sent.
  /// </summary>
  /// <param name="length">The size of the data in bytes.</param>
  void MarkBytesSent(int length);

  /// <summary>
  /// Records that a video frame has been sent.
  /// </summary>
  void MarkFrameSent();

  /// <summary>
  /// Sets a value indicating whether the GPU is being used for video encoding.
  /// </summary>
  /// <param name="isUsingGpu">True if the GPU is being used; otherwise, false.</param>
  void SetIsUsingGpu(bool isUsingGpu);
}
