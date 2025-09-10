using ControlR.DesktopClient.Common.Models;
using System.Drawing;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IScreenGrabber
{
  /// <summary>
  ///   Gets a capture of a specific display.
  /// </summary>
  /// <param name="targetDisplay">The display to capture.  Retrieve current displays from <see cref="GetDisplays" />. </param>
  /// <param name="captureCursor">Whether to include the cursor in the capture.</param>
  /// <param name="tryUseGpuAcceleration">Whether to attempt using GPU acceleration (e.g. DirectX) for getting the capture.</param>
  /// <param name="gpuCaptureTimeout">
  ///   The amount of time, in milliseconds, to allow GPU acceleration to attempt to capture the screen.
  ///   If no screen changes have occurred within this time, the capture will time out.
  /// </param>
  /// <param name="allowFallbackToCpu">
  ///   Whether to allow fallback to CPU-based capture, which is not GPU-accelerated, in the event of timeout or
  ///   exception.
  /// </param>
  /// <returns>
  ///   A result object indicating whether the capture was successful.
  ///   If successful, the result will contain the <see cref="SKBitmap" /> of the capture.
  /// </returns>
  CaptureResult Capture(
    DisplayInfo targetDisplay,
    bool captureCursor = true,
    bool tryUseGpuAcceleration = true,
    int gpuCaptureTimeout = 50,
    bool allowFallbackToCpu = true);

  /// <summary>
  ///   Gets a capture of all displays.  This method is not GPU-accelerated.
  /// </summary>
  /// <param name="captureCursor">Whether to include the cursor in the capture.</param>
  /// <returns>
  ///   A result object indicating whether the capture was successful.
  ///   If successful, the result will contain the <see cref="SKBitmap" /> of the capture.
  /// </returns>
  CaptureResult Capture(bool captureCursor = true);

  /// <summary>
  ///   Return info about the connected displays.
  /// </summary>
  /// <returns></returns>
  IEnumerable<DisplayInfo> GetDisplays();

  /// <summary>
  ///   Returns the area encompassing all displays.
  /// </summary>
  Rectangle GetVirtualScreenBounds();
}