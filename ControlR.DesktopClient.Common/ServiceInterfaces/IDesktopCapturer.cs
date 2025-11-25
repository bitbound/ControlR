using ControlR.DesktopClient.Common.Models;
using ControlR.Libraries.Shared.Dtos;
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
  /// Gets an asynchronous stream of captured screen regions.
  /// </summary>
  /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
  /// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="DtoWrapper"/>.</returns>
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
  /// Tries to get the display that is currently selected for capture.
  /// </summary>
  /// <param name="display">When this method returns, contains the selected display, if found; otherwise, null.</param>
  /// <returns>true if a display is selected; otherwise, false.</returns>
  bool TryGetSelectedDisplay([NotNullWhen(true)] out DisplayInfo? display);
}