using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace ControlR.Streamer.Models;

public sealed class CaptureResult : IDisposable
{
  public Bitmap? Bitmap { get; init; }

  public Rectangle[] DirtyRects { get; init; } = [];
  public CaptureResult? PreviousResult { get; init; }
  public bool DxTimedOut { get; init; }
  public Exception? Exception { get; init; }
  public string FailureReason { get; init; } = string.Empty;

  [MemberNotNullWhen(true, nameof(Exception))]
  public bool HadException => Exception is not null;

  public bool HadNoChanges { get; init; }

  [MemberNotNull(nameof(Bitmap))]
  public bool IsSuccess { get; init; }
  public bool IsUsingGpu { get; init; }
  public void Dispose()
  {
    Bitmap?.Dispose();
  }

  internal static CaptureResult Fail(string failureReason, CaptureResult? dxCaptureResult = null)
  {
    return new CaptureResult()
    {
      FailureReason = failureReason,
      PreviousResult = dxCaptureResult,
    };
  }

  internal static CaptureResult Fail(
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

  internal static CaptureResult NoAccumulatedFrames()
  {
    return new CaptureResult()
    {
      FailureReason = "No frames were accumulated.",
      DxTimedOut = true,
    };
  }

  internal static CaptureResult NoChanges()
  {
    return new CaptureResult()
    {
      HadNoChanges = true
    };
  }

  internal static CaptureResult Ok(
    Bitmap bitmap, 
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

  internal static CaptureResult TimedOut()
  {
    return new CaptureResult()
    {
      FailureReason = "Timed out while waiting for next frame",
      DxTimedOut = true,
    };
  }
}