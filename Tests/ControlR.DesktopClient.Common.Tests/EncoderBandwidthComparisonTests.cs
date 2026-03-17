using System.Diagnostics;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Common.Services.Encoders;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.TestingUtilities;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.Tests;

/// <summary>
///   Remote-control experiment for comparing image codecs on the same recorded
///   desktop frame sequence, diffing logic, and target FPS.
/// </summary>
public class EncoderBandwidthComparisonTests(ITestOutputHelper testOutput)
{
  private readonly SkiaSharpEncoder _encoder = new(new ImageUtility());
  private readonly ImageUtility _imageUtility = new();
  private readonly ITestOutputHelper _testOutput = testOutput;
  private readonly bool _useMultipleChangeAreas = true;

  [InteractiveWindowsFact]
  public void RecordedSession_ShouldReportCodecBandwidthAtFixedFps()
  {
    const int quality = 75;
    var sequence = RecordedDesktopSequence.Load();
    var formats = new[] { ImageFormat.Jpeg };
    var results = formats
      .Select(format => MeasureTransfer(sequence, quality, format))
      .OrderBy(result => result.TotalBytes)
      .ToArray();

    Assert.All(results, result =>
    {
      Assert.Equal(sequence.FrameCount, result.TotalFrames);
      Assert.Equal(1, result.KeyFrameCount);
      Assert.True(result.EncodedFrames > 1, "Expected at least one delta frame to be encoded.");
      Assert.True(result.TotalBytes > 0, "Encoded byte count should be positive.");
      Assert.True(result.AverageMbps > 0, "Calculated Mbps should be positive.");
      Assert.True(result.P95EncodeMilliseconds >= result.P50EncodeMilliseconds, "P95 should be greater than or equal to P50.");
    });

    _testOutput.WriteLine(
      $"Recorded session: {sequence.Width}x{sequence.Height} at {sequence.FramesPerSecond} FPS, quality {quality}, {sequence.FrameCount} frames over {sequence.DurationSeconds:F1} seconds.");

    foreach (var result in results)
    {
      _testOutput.WriteLine(result.ToDisplayString());
    }
  }

  private static SKBitmap CloneBitmap(SKBitmap source)
  {
    var clone = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
    using var canvas = new SKCanvas(clone);
    canvas.DrawBitmap(source, 0, 0);
    return clone;
  }

  private static double Percentile(IReadOnlyList<double> samples, double percentile)
  {
    if (samples.Count == 0)
    {
      return 0;
    }

    var ordered = samples.OrderBy(sample => sample).ToArray();
    var index = (int)Math.Ceiling(percentile * ordered.Length) - 1;
    return ordered[Math.Clamp(index, 0, ordered.Length - 1)];
  }

  private CodecTransferResult MeasureTransfer(RecordedDesktopSequenceInfo sequence, int quality, ImageFormat format)
  {
    long totalBytes = 0;
    long keyFrameBytes = 0;
    long deltaFrameBytes = 0;
    var keyFrameCount = 0;
    var deltaFrameCount = 0;
    var skippedFrameCount = 0;
    var frameCount = 0;
    var encodeDurations = new List<double>(sequence.FrameCount);
    SKBitmap? previousFrame = null;

    try
    {
      foreach (var framePath in sequence.FramePaths)
      {
        using var currentFrame = SKBitmap.Decode(framePath)
          ?? throw new InvalidOperationException($"Failed to decode frame '{framePath}'.");

        Assert.Equal(sequence.Width, currentFrame.Width);
        Assert.Equal(sequence.Height, currentFrame.Height);

        frameCount++;
        var stopwatch = Stopwatch.StartNew();

        if (previousFrame is null)
        {
          var encodedBytes = _encoder.EncodeFullFrame(currentFrame, quality, format);
          keyFrameCount++;
          keyFrameBytes += encodedBytes.Length;
          totalBytes += encodedBytes.Length;
        }
        else if (_useMultipleChangeAreas)
        {
          var changedAreasResult = _imageUtility.GetChangedAreas(currentFrame, previousFrame);
          Assert.True(changedAreasResult.IsSuccess, changedAreasResult.Reason);
          if (changedAreasResult.Value is { } changedAreas && changedAreas.Length > 0)
          {
            Parallel.ForEach(changedAreas, changedArea =>
            {
              var encodedBytes = _encoder.EncodeRegion(currentFrame, changedArea, quality, format);
              Interlocked.Exchange(ref totalBytes, totalBytes + encodedBytes.Length);
              Interlocked.Exchange(ref deltaFrameBytes, deltaFrameBytes + encodedBytes.Length);
            });

            deltaFrameCount++;
          }
          else
          {
            skippedFrameCount++;
          }
        }
        else
        {

          var changedAreaResult = _imageUtility.GetChangedArea(currentFrame, previousFrame);
          Assert.True(changedAreaResult.IsSuccess, changedAreaResult.Reason);

          if (changedAreaResult.Value is { } changedArea && !changedArea.IsEmpty)
          {
            var encodedBytes = _encoder.EncodeRegion(currentFrame, changedArea, quality, format);
            deltaFrameBytes += encodedBytes.Length;
            deltaFrameCount++;
            totalBytes += encodedBytes.Length;
          }
          else
          {
            skippedFrameCount++;
          }
        }

        stopwatch.Stop();
        encodeDurations.Add(stopwatch.Elapsed.TotalMilliseconds);

        previousFrame?.Dispose();
        previousFrame = CloneBitmap(currentFrame);
      }
    }
    finally
    {
      previousFrame?.Dispose();
    }

    var sessionSeconds = frameCount > 1
      ? (frameCount - 1) / (double)sequence.FramesPerSecond
      : 1d / sequence.FramesPerSecond;

    return new CodecTransferResult(
      Format: format,
      TotalFrames: frameCount,
      EncodedFrames: keyFrameCount + deltaFrameCount,
      KeyFrameCount: keyFrameCount,
      DeltaFrameCount: deltaFrameCount,
      SkippedFrameCount: skippedFrameCount,
      TotalBytes: totalBytes,
      KeyFrameBytes: keyFrameBytes,
      DeltaFrameBytes: deltaFrameBytes,
      AverageMbps: totalBytes * 8d / sessionSeconds / 1_000_000d,
      P50EncodeMilliseconds: Percentile(encodeDurations, 0.50),
      P95EncodeMilliseconds: Percentile(encodeDurations, 0.95));
  }

  private sealed record CodecTransferResult(
    ImageFormat Format,
    int TotalFrames,
    int EncodedFrames,
    int KeyFrameCount,
    int DeltaFrameCount,
    int SkippedFrameCount,
    long TotalBytes,
    long KeyFrameBytes,
    long DeltaFrameBytes,
    double AverageMbps,
    double P50EncodeMilliseconds,
    double P95EncodeMilliseconds)
  {
    public string ToDisplayString()
    {
      return
        $"{Format,-4} | total={TotalBytes,10:N0} bytes | avg={AverageMbps,7:F3} Mbps | " +
        $"key={KeyFrameBytes,9:N0} | delta={DeltaFrameBytes,9:N0} | encoded={EncodedFrames,3} | " +
        $"skipped={SkippedFrameCount,3} | p50={P50EncodeMilliseconds,6:F2} ms | p95={P95EncodeMilliseconds,6:F2} ms";
    }
  }

  private sealed record RecordedDesktopSequenceInfo(
    IReadOnlyList<string> FramePaths,
    int Width,
    int Height,
    int FramesPerSecond)
  {
    public int FrameCount => FramePaths.Count;

    public double DurationSeconds => Math.Max(0, FrameCount - 1) / (double)FramesPerSecond;
  }

  private static class RecordedDesktopSequence
  {
    public const int ExpectedFrameCount = 101;
    public const int FramesPerSecond = 10;

    public static RecordedDesktopSequenceInfo Load()
    {
      var solutionDirResult = IoHelper.GetSolutionDir(Directory.GetCurrentDirectory());
      Assert.True(solutionDirResult.IsSuccess, solutionDirResult.Reason);

      var resourcesDirectory = Path.Combine(
        solutionDirResult.Value,
        "Tests",
        "ControlR.DesktopClient.Common.Tests",
        "Resources");

      Assert.True(Directory.Exists(resourcesDirectory), $"Resource directory not found: {resourcesDirectory}");

      var framePaths = Directory
        .EnumerateFiles(resourcesDirectory, "Desktop_*.png")
        .OrderBy(static path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
        .ToArray();

      Assert.Equal(ExpectedFrameCount, framePaths.Length);
      Assert.Equal("Desktop_0001.png", Path.GetFileName(framePaths[0]));
      Assert.Equal("Desktop_0101.png", Path.GetFileName(framePaths[^1]));

      using var firstFrame = SKBitmap.Decode(framePaths[0])
        ?? throw new InvalidOperationException($"Failed to decode frame '{framePaths[0]}'.");

      Assert.True(firstFrame.Width > 0, "Frame width must be positive.");
      Assert.True(firstFrame.Height > 0, "Frame height must be positive.");

      return new RecordedDesktopSequenceInfo(framePaths, firstFrame.Width, firstFrame.Height, FramesPerSecond);
    }
  }
}
