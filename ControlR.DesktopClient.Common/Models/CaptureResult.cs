using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.Models;

public sealed class CaptureResult : IDisposable
{
  public SKBitmap? Bitmap { get; init; }

  public string? CaptureMode { get; set; }
  public Rectangle[]? DirtyRects { get; init; }
  public Exception? Exception { get; init; }
  public string FailureReason { get; init; } = string.Empty;

  [MemberNotNullWhen(true, nameof(Exception))]
  public bool HadException => Exception is not null;
  public bool HadNoChanges { get; private set; }

  [MemberNotNullWhen(true, nameof(Bitmap))]
  public bool IsSuccess { get; init; }
  public static CaptureResult Fail(string failureReason)
  {
    return new CaptureResult()
    {
      FailureReason = failureReason,
    };
  }

  public static CaptureResult Fail(
    Exception exception,
    string? failureReason = null)
  {
    return new CaptureResult()
    {
      FailureReason = failureReason ?? exception.Message,
      Exception = exception,
    };
  }

  public static CaptureResult NoChanges(string captureMode)
  {
    return new CaptureResult()
    {
      FailureReason = "No changes detected.",
      HadNoChanges = true,
      CaptureMode = captureMode
    };
  }

  public static CaptureResult Ok(
    SKBitmap bitmap,
    string captureMode,
    Rectangle[]? dirtyRects = null)
  {
    return new CaptureResult()
    {
      Bitmap = bitmap,
      IsSuccess = true,
      CaptureMode = captureMode,
      DirtyRects = dirtyRects,
    };
  }

  public void Dispose()
  {
    Bitmap?.Dispose();
  }
}