using ControlR.DesktopClient.Common.Models;
using System.Drawing;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IScreenGrabber
{
  /// <summary>
  ///   Gets a capture of all displays.  This method is not GPU-accelerated.
  /// </summary>
  /// <param name="captureCursor">Whether to include the cursor in the capture.</param>
  /// <returns>
  ///   A result object indicating whether the capture was successful.
  ///   If successful, the result will contain the <see cref="SKBitmap" /> of the capture.
  /// </returns>
  CaptureResult CaptureAllDisplays(bool captureCursor = true);

  /// <summary>
  ///   Gets a capture of a specific display.
  /// </summary>
  /// <param name="targetDisplay">The display to capture.  Retrieve current displays from <see cref="GetDisplays" />. </param>
  /// <param name="captureCursor">Whether to include the cursor in the capture.</param>
  /// <param name="forceKeyFrame">Whether to force a full frame capture, even if no changes have occurred since the last capture.</param>
  /// <returns>
  ///   A result object indicating whether the capture was successful.
  ///   If successful, the result will contain the <see cref="SKBitmap" /> of the capture.
  /// </returns>
  CaptureResult CaptureDisplay(
    DisplayInfo targetDisplay,
    bool captureCursor = true,
    bool forceKeyFrame = false);

  /// <summary>
  ///   Deinitializes the screen grabber, releasing any resources.
  /// </summary>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  // TODO: Replace with scoped services.
  Task Deinitialize(CancellationToken cancellationToken);

  /// <summary>
  ///   Initializes the screen grabber, preparing any necessary resources.
  /// </summary>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  Task Initialize(CancellationToken cancellationToken);
}