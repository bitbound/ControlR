using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using MessagePack;
using SkiaSharp;

namespace ControlR.Libraries.CaptureRecording.Tests;

public class CaptureRecorderPlayerTests
{
  [Fact]
  public async Task DisposedFrameRegistration_ShouldNotReceiveCallbacks()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    using var stream = new MemoryStream();

    await using (var recorder = new CaptureRecorder(new NonClosingStream(stream)))
    {
      await recorder.WriteFrame(
        CreateRegion(0, 0, 2, 2, SKColors.Red),
        new CaptureFrameMetadata
        {
          CanvasWidth = 2,
          CanvasHeight = 2,
          Timestamp = TimeSpan.Zero,
          IsKeyFrame = true
        },
        cancellationToken);
    }

    stream.Position = 0;

    await using var player = new CapturePlayer(new NonClosingStream(stream));
    var invocationCount = 0;

    var registration = player.OnFrameReady(frame =>
    {
      invocationCount++;
      frame.Dispose();
    });

    registration.Dispose();
    await player.Reset(cancellationToken);

    Assert.Equal(0, invocationCount);
  }

  [Fact]
  public async Task InvalidVersion_ShouldThrow()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    using var stream = new MemoryStream();

    await using (var recorder = new CaptureRecorder(new NonClosingStream(stream)))
    {
      await recorder.WriteFrame(
        CreateRegion(0, 0, 2, 2, SKColors.Red),
        new CaptureFrameMetadata
        {
          CanvasWidth = 2,
          CanvasHeight = 2,
          Timestamp = TimeSpan.Zero,
          IsKeyFrame = true
        },
        cancellationToken);
    }

    stream.Position = 0;
    stream.WriteByte(255);
    stream.WriteByte(0);
    stream.Position = 0;

    Assert.Throws<InvalidDataException>(() => new CapturePlayer(stream));
  }

  [Fact]
  public async Task RecorderAndPlayer_ShouldRoundTripFramesAndEvents()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    using var stream = new MemoryStream();

    await using (var recorder = new CaptureRecorder(new NonClosingStream(stream)))
    {
      await recorder.WriteFrame(
        CreateRegion(0, 0, 4, 4, SKColors.Red),
        new CaptureFrameMetadata
        {
          CanvasWidth = 4,
          CanvasHeight = 4,
          Timestamp = TimeSpan.Zero,
          IsKeyFrame = true
        },
        cancellationToken);

      await recorder.WriteEvent(
        "input",
        new TestEventPayload
        {
          Name = "click"
        },
        TimeSpan.FromMilliseconds(50),
        cancellationToken);

      await recorder.WriteFrame(
        CreateRegion(1, 1, 2, 2, SKColors.Blue),
        new CaptureFrameMetadata
        {
          CanvasWidth = 4,
          CanvasHeight = 4,
          Timestamp = TimeSpan.FromMilliseconds(100)
        },
        cancellationToken);
    }

    stream.Position = 0;

    await using var player = new CapturePlayer(new NonClosingStream(stream));

    var frameCount = 0;
    var eventPayloads = new List<TestEventPayload>();
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    using var frameRegistration = player.OnFrameReady(frame =>
    {
      frameCount++;

      if (frameCount == 1)
      {
        Assert.Equal(SKColors.Red, frame.Image.GetPixel(0, 0));
      }

      if (frameCount == 2)
      {
        Assert.Equal(SKColors.Red, frame.Image.GetPixel(0, 0));
        Assert.Equal(SKColors.Blue, frame.Image.GetPixel(1, 1));
        completed.TrySetResult();
      }

      frame.Dispose();
    });

    using var eventRegistration = player.OnEvent(playbackEvent =>
    {
      eventPayloads.Add(playbackEvent.GetPayload<TestEventPayload>());
    });

    await player.Start(cancellationToken);
    await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
    await player.Stop();

    Assert.Equal(2, frameCount);
    Assert.Single(eventPayloads);
    Assert.Equal("click", eventPayloads[0].Name);
  }

  [Fact]
  public async Task SeekAndReset_ShouldRebuildExpectedFrames()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    using var stream = new MemoryStream();

    await using (var recorder = new CaptureRecorder(new NonClosingStream(stream)))
    {
      await recorder.WriteFrame(
        CreateRegion(0, 0, 4, 4, SKColors.Red),
        new CaptureFrameMetadata
        {
          CanvasWidth = 4,
          CanvasHeight = 4,
          Timestamp = TimeSpan.Zero,
          IsKeyFrame = true
        },
        cancellationToken);

      await recorder.WriteFrame(
        CreateRegion(2, 0, 2, 4, SKColors.Green),
        new CaptureFrameMetadata
        {
          CanvasWidth = 4,
          CanvasHeight = 4,
          Timestamp = TimeSpan.FromMilliseconds(100)
        },
        cancellationToken);
    }

    stream.Position = 0;

    await using var player = new CapturePlayer(new NonClosingStream(stream));
    CapturePlaybackFrame? latestFrame = null;

    using var registration = player.OnFrameReady(frame =>
    {
      latestFrame?.Dispose();
      latestFrame = frame;
    });

    await player.Seek(TimeSpan.FromMilliseconds(100), cancellationToken);

    Assert.NotNull(latestFrame);
    Assert.Equal(SKColors.Green, latestFrame.Image.GetPixel(3, 1));
    Assert.Equal(SKColors.Red, latestFrame.Image.GetPixel(0, 1));

    await player.Reset(cancellationToken);

    Assert.NotNull(latestFrame);
    Assert.Equal(SKColors.Red, latestFrame.Image.GetPixel(0, 1));
    Assert.Equal(SKColors.Red, latestFrame.Image.GetPixel(3, 1));

    latestFrame.Dispose();
  }

  [Fact]
  public async Task TruncatedRecording_ShouldThrowWhenReadingFrames()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    using var stream = new MemoryStream();

    await using (var recorder = new CaptureRecorder(new NonClosingStream(stream)))
    {
      await recorder.WriteFrame(
        CreateRegion(0, 0, 4, 4, SKColors.Red),
        new CaptureFrameMetadata
        {
          CanvasWidth = 4,
          CanvasHeight = 4,
          Timestamp = TimeSpan.Zero,
          IsKeyFrame = true
        },
        cancellationToken);

      await recorder.WriteFrame(
        CreateRegion(0, 0, 4, 4, SKColors.Blue),
        new CaptureFrameMetadata
        {
          CanvasWidth = 4,
          CanvasHeight = 4,
          Timestamp = TimeSpan.FromMilliseconds(100)
        },
        cancellationToken);
    }

    var bytes = stream.ToArray();
    Array.Resize(ref bytes, bytes.Length - 8);
    using var truncatedStream = new MemoryStream(bytes);

    Assert.ThrowsAny<Exception>(() => new CapturePlayer(new NonClosingStream(truncatedStream)));
  }

  private static ScreenRegionDto CreateRegion(int x, int y, int width, int height, SKColor color)
  {
    using var bitmap = new SKBitmap(width, height, true);
    bitmap.Erase(color);
    using var image = SKImage.FromBitmap(bitmap);
    using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

    return new ScreenRegionDto(
      x,
      y,
      width,
      height,
      encoded.ToArray(),
      ImageFormat.Png);
  }

  [MessagePackObject(keyAsPropertyName: true, AllowPrivate = true)]
  internal sealed class TestEventPayload
  {
    public string Name { get; init; } = string.Empty;
  }

  private sealed class NonClosingStream(Stream innerStream) : Stream
  {
    private readonly Stream _innerStream = innerStream;

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => _innerStream.CanSeek;
    public override bool CanWrite => _innerStream.CanWrite;
    public override long Length => _innerStream.Length;
    public override long Position
    {
      get => _innerStream.Position;
      set => _innerStream.Position = value;
    }

    public override ValueTask DisposeAsync()
    {
      _innerStream.Flush();
      return ValueTask.CompletedTask;
    }

    public override void Flush()
    {
      _innerStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      return _innerStream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      return _innerStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
      _innerStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      _innerStream.Write(buffer, offset, count);
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        _innerStream.Flush();
      }

      base.Dispose(disposing);
    }
  }
}
