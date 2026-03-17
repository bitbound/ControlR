using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using MessagePack;
using SkiaSharp;

namespace ControlR.Libraries.CaptureRecording;

public sealed class CaptureRecorder : IAsyncDisposable
{
  private readonly List<CaptureIndexEntry> _indexEntries = [];
  private readonly CaptureRecorderOptions _options;
  private readonly SKPaint _paint = new();
  private readonly DateTimeOffset _recordingStartedAt;
  private readonly Stream _stream;
  private readonly TimeProvider _timeProvider;
  private readonly Lock _writeLock = new();

  private SKBitmap? _compositedFrame;
  private bool _disposed;
  private int _framesSinceKeyFrame;
  private long _lastKeyFrameOffset = -1;
  private TimeSpan _lastKeyFrameTimestamp = TimeSpan.MinValue;
  private TimeSpan _lastTimestamp;
  private int _sequence;

  public CaptureRecorder(
    Stream stream,
    TimeProvider? timeProvider = null,
    CaptureRecorderOptions? options = null)
  {
    ArgumentNullException.ThrowIfNull(stream);

    if (!stream.CanSeek)
    {
      throw new ArgumentException("CaptureRecorder requires a seekable stream.", nameof(stream));
    }

    if (!stream.CanWrite)
    {
      throw new ArgumentException("CaptureRecorder requires a writable stream.", nameof(stream));
    }

    _stream = stream;
    _timeProvider = timeProvider ?? TimeProvider.System;
    _options = options ?? new();
    _recordingStartedAt = _timeProvider.GetUtcNow();

    _stream.SetLength(0);
    CaptureRecordingStorage.WriteFileHeader(
      _stream,
      new CaptureFileHeader(
        CaptureRecordingStorage.CurrentVersion,
        CaptureRecordingStorage.FileHeaderSize,
        CaptureRecordingStorage.FileHeaderSize,
        _recordingStartedAt.UtcTicks,
        0,
        0,
        0));
    _stream.Position = CaptureRecordingStorage.FileHeaderSize;
  }

  public async ValueTask DisposeAsync()
  {
    if (_disposed)
    {
      return;
    }

    _disposed = true;

    try
    {
      FinalizeIndex();
      await _stream.FlushAsync().ConfigureAwait(false);
    }
    finally
    {
      _compositedFrame?.Dispose();
      _paint.Dispose();
      await _stream.DisposeAsync().ConfigureAwait(false);
      GC.SuppressFinalize(this);
    }
  }

  public Task WriteEvent<T>(
    string eventType,
    T payload,
    TimeSpan? timestamp = null,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
    ObjectDisposedException.ThrowIf(_disposed, this);
    cancellationToken.ThrowIfCancellationRequested();

    using var writeLock = _writeLock.EnterScope();

    var recordTimestamp = NormalizeTimestamp(timestamp);
    var serializedPayload = MessagePackSerializer.Serialize(payload, cancellationToken: cancellationToken);
    var payloadData = new CaptureEventRecordData
    {
      EventType = eventType,
      Payload = serializedPayload,
      PayloadType = typeof(T).FullName ?? string.Empty
    };

    var payloadBytes = MessagePackSerializer.Serialize(payloadData, cancellationToken: cancellationToken);
    WriteRecord(
      CaptureRecordKind.Event,
      recordTimestamp,
      payloadBytes,
      canvasWidth: 0,
      canvasHeight: 0);

    return Task.CompletedTask;
  }

  public Task WriteFrame(
    ScreenRegionDto region,
    CaptureFrameMetadata metadata,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(region);
    return WriteFrame([region], metadata, cancellationToken);
  }

  public Task WriteFrame(
    ScreenRegionsDto regions,
    CaptureFrameMetadata metadata,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(regions);
    return WriteFrame(regions.Regions, metadata, cancellationToken);
  }

  public Task WriteFrame(
    IReadOnlyCollection<ScreenRegionDto> regions,
    CaptureFrameMetadata metadata,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(regions);
    ArgumentNullException.ThrowIfNull(metadata);
    ObjectDisposedException.ThrowIf(_disposed, this);
    cancellationToken.ThrowIfCancellationRequested();

    if (regions.Count == 0)
    {
      throw new ArgumentException("At least one screen region is required.", nameof(regions));
    }

    if (metadata.CanvasWidth <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(metadata), "CanvasWidth must be greater than zero.");
    }

    if (metadata.CanvasHeight <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(metadata), "CanvasHeight must be greater than zero.");
    }

    using var writeLock = _writeLock.EnterScope();

    var recordTimestamp = NormalizeTimestamp(metadata.Timestamp);
    var regionArray = regions as ScreenRegionDto[] ?? [.. regions];

    ApplyRegions(regionArray, metadata.CanvasWidth, metadata.CanvasHeight);

    if (ShouldWriteKeyFrame(recordTimestamp, metadata))
    {
      _lastKeyFrameOffset = WriteKeyFrame(recordTimestamp, metadata, cancellationToken);
      _lastKeyFrameTimestamp = recordTimestamp;
      _framesSinceKeyFrame = 0;
    }

    var payload = MessagePackSerializer.Serialize(
      new CaptureFrameRecordData
      {
        CaptureMode = metadata.CaptureMode,
        Regions = regionArray
      },
      cancellationToken: cancellationToken);

    WriteRecord(
      CaptureRecordKind.FrameBatch,
      recordTimestamp,
      payload,
      metadata.CanvasWidth,
      metadata.CanvasHeight);

    _framesSinceKeyFrame++;
    return Task.CompletedTask;
  }

  private static SKEncodedImageFormat ToSkFormat(ImageFormat format)
  {
    return format switch
    {
      ImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
      ImageFormat.Png => SKEncodedImageFormat.Png,
      ImageFormat.WebP => SKEncodedImageFormat.Webp,
      _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
    };
  }

  private void ApplyRegions(
    IReadOnlyCollection<ScreenRegionDto> regions,
    int canvasWidth,
    int canvasHeight)
  {
    EnsureCanvasSize(canvasWidth, canvasHeight);

    if (_compositedFrame is null)
    {
      throw new InvalidOperationException("Composited frame could not be initialized.");
    }

    using var canvas = new SKCanvas(_compositedFrame);

    foreach (var region in regions)
    {
      using var decoded = SKBitmap.Decode(region.EncodedImage);
      if (decoded is null)
      {
        throw new InvalidDataException("Screen region image could not be decoded.");
      }

      canvas.DrawBitmap(decoded, region.X, region.Y, _paint);
    }
  }

  private void EnsureCanvasSize(int canvasWidth, int canvasHeight)
  {
    if (_compositedFrame is not null &&
        _compositedFrame.Width == canvasWidth &&
        _compositedFrame.Height == canvasHeight)
    {
      return;
    }

    _compositedFrame?.Dispose();
    _compositedFrame = new SKBitmap(canvasWidth, canvasHeight, true);
    _compositedFrame.Erase(SKColors.Transparent);
  }

  private void FinalizeIndex()
  {
    using var writeLock = _writeLock.EnterScope();

    var indexOffset = _stream.Position;
    foreach (var entry in _indexEntries)
    {
      CaptureRecordingStorage.WriteIndexEntry(_stream, entry);
    }

    var fileLength = _stream.Position;

    CaptureRecordingStorage.WriteFileHeader(
      _stream,
      new CaptureFileHeader(
        CaptureRecordingStorage.CurrentVersion,
        CaptureRecordingStorage.FileHeaderSize,
        fileLength,
        _recordingStartedAt.UtcTicks,
        indexOffset,
        _indexEntries.Count,
        0));

    _stream.Position = fileLength;
  }

  private TimeSpan NormalizeTimestamp(TimeSpan? timestamp)
  {
    var normalized = timestamp ?? (_timeProvider.GetUtcNow() - _recordingStartedAt);
    if (normalized < TimeSpan.Zero)
    {
      normalized = TimeSpan.Zero;
    }

    if (normalized < _lastTimestamp)
    {
      normalized = _lastTimestamp;
    }

    _lastTimestamp = normalized;
    return normalized;
  }

  private bool ShouldWriteKeyFrame(TimeSpan timestamp, CaptureFrameMetadata metadata)
  {
    if (metadata.IsKeyFrame)
    {
      return true;
    }

    if (_lastKeyFrameOffset < 0)
    {
      return true;
    }

    if (_options.KeyFrameFrameInterval > 0 &&
        _framesSinceKeyFrame >= _options.KeyFrameFrameInterval)
    {
      return true;
    }

    if (_options.KeyFrameInterval <= TimeSpan.Zero)
    {
      return false;
    }

    return timestamp - _lastKeyFrameTimestamp >= _options.KeyFrameInterval;
  }

  private long WriteKeyFrame(
    TimeSpan timestamp,
    CaptureFrameMetadata metadata,
    CancellationToken cancellationToken)
  {
    if (_compositedFrame is null)
    {
      throw new InvalidOperationException("A key frame cannot be created before any frame data is available.");
    }

    using var image = SKImage.FromBitmap(_compositedFrame);
    using var encoded = image.Encode(
      metadata.IsKeyFrame ? SKEncodedImageFormat.Png : ToSkFormat(_options.KeyFrameImageFormat),
      _options.KeyFrameQuality);
    var payload = MessagePackSerializer.Serialize(
      new CaptureKeyFrameRecordData
      {
        CaptureMode = metadata.CaptureMode,
        EncodedImage = encoded.ToArray(),
        ImageFormat = metadata.IsKeyFrame ? ImageFormat.Png : _options.KeyFrameImageFormat
      },
      cancellationToken: cancellationToken);

    var offset = _stream.Position;
    WriteRecord(
      CaptureRecordKind.KeyFrame,
      timestamp,
      payload,
      metadata.CanvasWidth,
      metadata.CanvasHeight);

    return offset;
  }

  private void WriteRecord(
    CaptureRecordKind kind,
    TimeSpan timestamp,
    byte[] payload,
    int canvasWidth,
    int canvasHeight)
  {
    var offset = _stream.Position;
    var sequence = ++_sequence;
    var nearestKeyFrameOffset = kind == CaptureRecordKind.KeyFrame
      ? offset
      : _lastKeyFrameOffset;

    var header = new CaptureRecordHeader(
      kind,
      CaptureRecordFlags.None,
      sequence,
      timestamp.Ticks,
      payload.Length,
      canvasWidth,
      canvasHeight);

    CaptureRecordingStorage.WriteRecord(_stream, header, payload);

    _indexEntries.Add(
      new CaptureIndexEntry(
        offset,
        timestamp.Ticks,
        sequence,
        nearestKeyFrameOffset,
        kind));
  }
}
