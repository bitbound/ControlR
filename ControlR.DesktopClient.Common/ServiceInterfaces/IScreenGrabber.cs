using ControlR.DesktopClient.Common.Models;
using System.Drawing;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IScreenGrabber
{
  /// <summary>
  ///   Gets a capture of a specific display.
  /// </summary>
  /// <param name="targetDisplay">The display to capture.  Retrieve current displays from <see cref="GetDisplays" />. </param>
  /// <param name="captureCursor">Whether to include the cursor in the capture.</param>
  /// <param name="tryUseDirectX">Whether to attempt using DirectX (DXGI) for getting the capture.</param>
  /// <param name="directXTimeout">
  ///   The amount of time, in milliseconds, to allow DirectX to attempt to capture the screen.
  ///   If no screen changes have occurred within this time, the capture will time out.
  /// </param>
  /// <param name="allowFallbackToBitBlt">
  ///   Whether to allow fallback to BitBlt for capture, which is not DirectX-accelerated, in the event of timeout or
  ///   exception.
  /// </param>
  /// <returns>
  ///   A result object indicating whether the capture was successful.
  ///   If successful, the result will contain the <see cref="Bitmap" /> of the capture.
  /// </returns>
  CaptureResult Capture(
    DisplayInfo targetDisplay,
    bool captureCursor = true,
    bool tryUseDirectX = true,
    int directXTimeout = 50,
    bool allowFallbackToBitBlt = true);

  /// <summary>
  ///   Gets a capture of all displays.  This method is not DirectX-accelerated.
  /// </summary>
  /// <param name="captureCursor">Whether to include the cursor in the capture.</param>
  /// <returns>
  ///   A result object indicating whether the capture was successful.
  ///   If successful, the result will contain the <see cref="Bitmap" /> of the capture.
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