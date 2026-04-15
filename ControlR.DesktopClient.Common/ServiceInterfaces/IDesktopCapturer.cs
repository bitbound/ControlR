using ControlR.DesktopClient.Common.Models;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Primitives;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;
/// <summary>
/// Defines a service for capturing and streaming desktop content.
/// </summary>
public interface IDesktopCapturer : IAsyncDisposable
{
  /// <summary>
  /// Switches the capture target to the specified display.
  /// </summary>
  /// <param name="displayId">The unique identifier of the target display.</param>
  /// <returns>A task representing the switch operation.</returns>
  Task ChangeDisplays(string displayId);

  /// <summary>
  /// Identifies the underlying capture technology currently in use.
  /// </summary>
  /// <returns>A string naming the capture mode (e.g., "GDI" or "DirectX").</returns>
  string GetCaptureMode();

  /// <summary>
  /// Provides an asynchronous stream of encoded screen data.
  /// </summary>
  /// <param name="cancellationToken">A token to terminate the stream.</param>
  /// <returns>An async enumerable of DTOs containing screen regions or frames.</returns>
  IAsyncEnumerable<DtoWrapper> GetCaptureStream(CancellationToken cancellationToken);

  /// <summary>
  /// Calculates the average frames per second over a specific time interval.
  /// </summary>
  /// <param name="window">The duration to analyze. Must be greater than zero.</param>
  /// <returns>The measured FPS, or 0 if no frames were sent during the window.</returns>
  double GetCurrentFps(TimeSpan window);

  /// <summary>
  /// Returns the current image encoding quality.
  /// </summary>
  /// <returns>The effective quality level, ranging from 1 to 100.</returns>
  int GetCurrentQuality();

  /// <summary>
  /// Forces the next capture to be a full key frame rather than a delta update.
  /// </summary>
  /// <returns>A task representing the request.</returns>
  Task RequestKeyFrame();

  /// <summary>
  /// Begins the screen capture and streaming loop.
  /// </summary>
  /// <param name="cancellationToken">A token to stop the capture process.</param>
  /// <returns>A task that completes when the capture starts.</returns>
  Task StartCapturingChanges(CancellationToken cancellationToken);

  /// <summary>
  /// Attempts to retrieve metadata for the display currently being captured.
  /// </summary>
  /// <returns>A result containing display information if a selection exists.</returns>
  Task<Result<DisplayInfo>> TryGetSelectedDisplay();
}