using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.Services;

public interface IDesktopPreviewProvider
{
  Task<Result<byte[]>> CapturePreview(int jpegQuality = 80);
  Task<Result<byte[]>> CapturePreview(int width, int height, int jpegQuality = 80);
}

public class DesktopPreviewProvider(
  TimeProvider timeProvider,
  IScreenGrabber screenGrabber,
  ILogger<DesktopPreviewProvider> logger) : IDesktopPreviewProvider
{
  private readonly SemaphoreSlim _captureLock = new(1, 1);
  private readonly IScreenGrabber _screenGrabber = screenGrabber;
  private readonly ILogger<DesktopPreviewProvider> _logger = logger;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task<Result<byte[]>> CapturePreview(int jpegQuality = 80)
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15), _timeProvider);
    try
    {
      using var held = await _captureLock.AcquireLockAsync(cts.Token);

      // Ensure screen grabber is initialized (idempotent)
      await _screenGrabber.Initialize(cts.Token);

      var result = _screenGrabber.CaptureAllDisplays(captureCursor: false);
      if (!result.IsSuccess || result.Bitmap is null)
      {
        return Result.Fail<byte[]>(result.FailureReason ?? "Failed to capture screen for preview");
      }

      using var originalBitmap = result.Bitmap;
      using var image = SKImage.FromBitmap(originalBitmap);
      using var data = image.Encode(SKEncodedImageFormat.Jpeg, jpegQuality);
      
      return Result.Ok(data.ToArray());
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error generating desktop preview");
      return Result.Fail<byte[]>(ex);
    }
    finally
    {
      await _screenGrabber.Uninitialize(cts.Token);
    }
  }

  public async Task<Result<byte[]>> CapturePreview(int width, int height, int jpegQuality = 80)
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15), _timeProvider);
    try
    {
      // Ensure screen grabber is initialized (idempotent)
      using var held = await _captureLock.AcquireLockAsync(cts.Token);

      await _screenGrabber.Initialize(cts.Token);

      var result = _screenGrabber.CaptureAllDisplays(captureCursor: false);
      if (!result.IsSuccess || result.Bitmap is null)
      {
        return Result.Fail<byte[]>(result.FailureReason ?? "Failed to capture screen for preview");
      }

      using var originalBitmap = result.Bitmap;
      
      // Calculate aspect-correct dimensions
      var scale = Math.Min((float)width / originalBitmap.Width, (float)height / originalBitmap.Height);
      var newWidth = (int)(originalBitmap.Width * scale);
      var newHeight = (int)(originalBitmap.Height * scale);

      var info = new SKImageInfo(newWidth, newHeight);
      using var resizedBitmap = originalBitmap.Resize(info, SKSamplingOptions.Default);
      
      if (resizedBitmap is null)
      {
        return Result.Fail<byte[]>("Failed to resize preview image");
      }

      using var image = SKImage.FromBitmap(resizedBitmap);
      using var data = image.Encode(SKEncodedImageFormat.Jpeg, jpegQuality);
      
      return Result.Ok(data.ToArray());
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error generating desktop preview");
      return Result.Fail<byte[]>(ex);
    }
    finally
    {
      await _screenGrabber.Uninitialize(cts.Token);
    }
  }
}
