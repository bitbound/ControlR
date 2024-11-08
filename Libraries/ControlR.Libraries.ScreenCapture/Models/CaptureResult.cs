using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace ControlR.Libraries.ScreenCapture.Models;

public sealed class CaptureResult : IDisposable
{
    public Bitmap? Bitmap { get; init; }

    public Rectangle[] DirtyRects { get; init; } = [];
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

    internal static CaptureResult Fail(string failureReason)
    {
        return new CaptureResult()
        {
            FailureReason = failureReason
        };
    }

    internal static CaptureResult Fail(Exception exception, string? failureReason = null)
    {
        return new CaptureResult()
        {
            FailureReason = failureReason ?? exception.Message,
            Exception = exception,
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

    internal static CaptureResult Ok(Bitmap bitmap, bool isUsingGpu, Rectangle[]? dirtyRects = default)
    {
        return new CaptureResult()
        {
            Bitmap = bitmap,
            IsSuccess = true,
            IsUsingGpu = isUsingGpu,
            DirtyRects = dirtyRects ?? []
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