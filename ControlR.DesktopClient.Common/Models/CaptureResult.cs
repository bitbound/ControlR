using SkiaSharp;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace ControlR.DesktopClient.Common.Models;

public sealed class CaptureResult : IDisposable
{
  public SKBitmap? Bitmap { get; init; }

  public Rectangle[] DirtyRects { get; init; } = [];
  public CaptureResult? PreviousResult { get; init; }
  public Exception? Exception { get; init; }
  public string FailureReason { get; init; } = string.Empty;

  [MemberNotNullWhen(true, nameof(Exception))]
  public bool HadException => Exception is not null;

  [MemberNotNull(nameof(Bitmap))]
  public bool IsSuccess { get; init; }
  public bool IsUsingGpu { get; init; }
  public void Dispose()
  {
    Bitmap?.Dispose();
  }

  public static CaptureResult Fail(string failureReason, CaptureResult? previousResult = null)
  {
    return new CaptureResult()
    {
      FailureReason = failureReason,
      PreviousResult = previousResult,
    };
  }

  public static CaptureResult Fail(
    Exception exception, 
    string? failureReason = null, 
    CaptureResult? dxCaptureResult = null)
  {
    return new CaptureResult()
    {
      FailureReason = failureReason ?? exception.Message,
      Exception = exception,
      PreviousResult = dxCaptureResult,
    };
  }

  public static CaptureResult Ok(
    SKBitmap bitmap, 
    bool isUsingGpu, 
    Rectangle[]? dirtyRects = default,
    CaptureResult? dxCaptureResult = null)
  {
    return new CaptureResult()
    {
      Bitmap = bitmap,
      IsSuccess = true,
      IsUsingGpu = isUsingGpu,
      DirtyRects = dirtyRects ?? [],
      PreviousResult = dxCaptureResult,
    };
  }

  public static CaptureResult TimedOut()
  {
    return new CaptureResult()
    {
      FailureReason = "Timed out while waiting for next frame.",
    };
  }
}