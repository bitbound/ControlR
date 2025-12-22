using ControlR.DesktopClient.Common.Models;
using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Primitives;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;
/// <summary>
/// Responsible for capturing the desktop and streaming it to a consumer.
/// </summary>
public interface IDesktopCapturer : IAsyncDisposable
{
  /// <summary>
  /// Changes the display that is being captured.
  /// </summary>
  /// <param name="displayId">The ID of the display to switch to.</param>
  /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
  Task ChangeDisplays(string displayId);

 /// <summary>
  /// Gets a stream of either JPEG frames or video, depending on the implementation.
  /// </summary>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  IAsyncEnumerable<DtoWrapper> GetCaptureStream(CancellationToken cancellationToken);

  /// <summary>
  /// Forces the next frame to be a key frame.
  /// </summary>
  /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
  Task RequestKeyFrame();

  /// <summary>
  /// Starts the process of capturing screen changes.
  /// </summary>
  /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
  /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
  Task StartCapturingChanges(CancellationToken cancellationToken);

  /// <summary>
  ///   Attempts to get the display that is currently selected for capture.
  /// </summary>
  /// <returns>
  ///   A <see cref="Task"/> representing the asynchronous operation, containing a <see cref="Result{DisplayInfo}"/> with the selected display if successful.
  /// </returns>
  Task<Result<DisplayInfo>> TryGetSelectedDisplay();
}