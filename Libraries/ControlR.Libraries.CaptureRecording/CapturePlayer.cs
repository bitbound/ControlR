using ControlR.Libraries.Shared.Collections;
using MessagePack;
using SkiaSharp;

namespace ControlR.Libraries.CaptureRecording;

public sealed class CapturePlayer : IAsyncDisposable
{
  private readonly HandlerCollection<CapturePlaybackEvent> _eventHandlers = new();
  private readonly HandlerCollection<CapturePlaybackFrame> _frameHandlers = new();
  private readonly List<CaptureIndexEntry> _indexEntries;
  private readonly CapturePlayerOptions _options;
  private readonly SemaphoreSlim _stateLock = new(1, 1);
  private readonly Stream _stream;
  private readonly TimeProvider _timeProvider;

  private SKBitmap? _compositedFrame;
  private int _currentRecordIndex;
  private bool _disposed;
  private CancellationTokenSource? _playbackCts;
  private Task? _playbackTask;
  private TimeSpan _position;

  public CapturePlayer(
    Stream stream,
    TimeProvider? timeProvider = null,
    CapturePlayerOptions? options = null)
  {
    ArgumentNullException.ThrowIfNull(stream);

    if (!stream.CanSeek)
    {
      throw new ArgumentException("CapturePlayer requires a seekable stream.", nameof(stream));
    }

    if (!stream.CanRead)
    {
      throw new ArgumentException("CapturePlayer requires a readable stream.", nameof(stream));
    }

    _stream = stream;
    _timeProvider = timeProvider ?? TimeProvider.System;
    _options = options ?? new();

    var header = CaptureRecordingStorage.ReadFileHeader(_stream);
    CaptureRecordingStorage.ValidateFileHeader(header);
    _indexEntries = LoadIndex(header);
    _currentRecordIndex = 0;
    _position = TimeSpan.Zero;
  }

  public TimeSpan Position => _position;

  public async ValueTask DisposeAsync()
  {
    if (_disposed)
    {
      return;
    }

    _disposed = true;
    await Stop().ConfigureAwait(false);
    _compositedFrame?.Dispose();
    _stateLock.Dispose();
    await _stream.DisposeAsync().ConfigureAwait(false);
    GC.SuppressFinalize(this);
  }

  public IDisposable OnEvent(Action<CapturePlaybackEvent> callback)
  {
    ArgumentNullException.ThrowIfNull(callback);

    var subscriber = new object();
    return _eventHandlers.AddHandler(
      subscriber,
      playbackEvent =>
      {
        callback(playbackEvent);
        return Task.CompletedTask;
      });
  }

  public IDisposable OnEvent(Func<CapturePlaybackEvent, Task> callback)
  {
    ArgumentNullException.ThrowIfNull(callback);

    var subscriber = new object();
    return _eventHandlers.AddHandler(subscriber, callback);
  }

  public IDisposable OnFrameReady(Action<CapturePlaybackFrame> callback)
  {
    ArgumentNullException.ThrowIfNull(callback);

    var subscriber = new object();
    return _frameHandlers.AddHandler(
      subscriber,
      frame =>
      {
        callback(frame);
        return Task.CompletedTask;
      });
  }

  public IDisposable OnFrameReady(Func<CapturePlaybackFrame, Task> callback)
  {
    ArgumentNullException.ThrowIfNull(callback);

    var subscriber = new object();
    return _frameHandlers.AddHandler(subscriber, callback);
  }

  public async Task Reset(CancellationToken cancellationToken = default)
  {
    await Seek(TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
  }

  public async Task Seek(TimeSpan position, CancellationToken cancellationToken = default)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    await Stop().ConfigureAwait(false);
    await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);

    try
    {
      await RebuildState(position, cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      _stateLock.Release();
    }
  }

  public async Task Start(CancellationToken cancellationToken = default)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);

    try
    {
      if (_playbackTask is { IsCompleted: false })
      {
        return;
      }

      _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      _playbackTask = RunPlayback(_playbackCts.Token);
    }
    finally
    {
      _stateLock.Release();
    }
  }

  public async Task Stop()
  {
    Task? taskToAwait = null;
    CancellationTokenSource? ctsToDispose = null;

    await _stateLock.WaitAsync().ConfigureAwait(false);

    try
    {
      if (_playbackCts is null)
      {
        return;
      }

      _playbackCts.Cancel();
      ctsToDispose = _playbackCts;
      taskToAwait = _playbackTask;
      _playbackCts = null;
      _playbackTask = null;
    }
    finally
    {
      _stateLock.Release();
    }

    try
    {
      if (taskToAwait is not null)
      {
        await taskToAwait.ConfigureAwait(false);
      }
    }
    catch (OperationCanceledException)
    {
    }
    finally
    {
      ctsToDispose?.Dispose();
    }
  }

  private static SKBitmap CloneBitmap(SKBitmap source)
  {
    var clone = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
    using var canvas = new SKCanvas(clone);
    canvas.DrawBitmap(source, 0, 0);
    return clone;
  }

  private async Task ApplyEvent(CaptureRecord record)
  {
    var payload = MessagePackSerializer.Deserialize<CaptureEventRecordData>(record.Payload);
    await _eventHandlers.InvokeHandlers(
      new CapturePlaybackEvent
      {
        EventType = payload.EventType,
        Payload = payload.Payload,
        PayloadType = payload.PayloadType,
        Sequence = record.Header.Sequence,
        Timestamp = TimeSpan.FromTicks(record.Header.TimestampTicks)
      },
      CancellationToken.None).ConfigureAwait(false);
  }

  private void ApplyFrameBatch(CaptureRecord record)
  {
    var payload = MessagePackSerializer.Deserialize<CaptureFrameRecordData>(record.Payload);

    EnsureCanvasSize(record.Header.CanvasWidth, record.Header.CanvasHeight);

    if (_compositedFrame is null)
    {
      throw new InvalidOperationException("Composited frame could not be initialized.");
    }

    using var canvas = new SKCanvas(_compositedFrame);
    foreach (var region in payload.Regions)
    {
      using var decoded = SKBitmap.Decode(region.EncodedImage) 
        ?? throw new InvalidDataException("Screen region image could not be decoded during playback.");

      canvas.DrawBitmap(decoded, region.X, region.Y);
    }
  }

  private void ApplyKeyFrame(CaptureRecord record)
  {
    var payload = MessagePackSerializer.Deserialize<CaptureKeyFrameRecordData>(record.Payload);
    using var encodedData = SKData.CreateCopy(payload.EncodedImage);
    using var image = SKImage.FromEncodedData(encodedData) 
      ?? throw new InvalidDataException("Key frame image could not be decoded during playback.");
    var bitmap = SKBitmap.FromImage(image);
    _compositedFrame?.Dispose();
    _compositedFrame = bitmap;
  }

  private async Task EmitFrame(int sequence, TimeSpan timestamp)
  {
    if (_compositedFrame is null)
    {
      return;
    }

    var clonedBitmap = CloneBitmap(_compositedFrame);
    await _frameHandlers.InvokeHandlers(
      new CapturePlaybackFrame
      {
        Image = clonedBitmap,
        Sequence = sequence,
        Timestamp = timestamp
      },
      CancellationToken.None).ConfigureAwait(false);
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

  private List<CaptureIndexEntry> LoadIndex(CaptureFileHeader header)
  {
    var entries = new List<CaptureIndexEntry>();

    if (header.IndexOffset > 0 && header.IndexEntryCount > 0)
    {
      _stream.Position = header.IndexOffset;
      for (var i = 0; i < header.IndexEntryCount; i++)
      {
        entries.Add(CaptureRecordingStorage.ReadIndexEntry(_stream));
      }

      return entries;
    }

    _stream.Position = CaptureRecordingStorage.FileHeaderSize;
    var nearestKeyFrameOffset = -1L;

    while (_stream.Position < _stream.Length)
    {
      var offset = _stream.Position;
      var record = CaptureRecordingStorage.ReadRecord(_stream, offset);
      if (record.Header.Kind == CaptureRecordKind.KeyFrame)
      {
        nearestKeyFrameOffset = offset;
      }

      entries.Add(
        new CaptureIndexEntry(
          offset,
          record.Header.TimestampTicks,
          record.Header.Sequence,
          nearestKeyFrameOffset,
          record.Header.Kind));

      _stream.Position = offset + CaptureRecordingStorage.RecordHeaderSize + record.Header.PayloadLength;
    }

    return entries;
  }

  private async Task RebuildState(TimeSpan position, CancellationToken cancellationToken)
  {
    var normalizedPosition = position < TimeSpan.Zero ? TimeSpan.Zero : position;

    _compositedFrame?.Dispose();
    _compositedFrame = null;

    var startEntryIndex = 0;
    var targetIndex = _indexEntries.FindIndex(entry => entry.TimestampTicks > normalizedPosition.Ticks);
    if (targetIndex < 0)
    {
      targetIndex = _indexEntries.Count;
    }

    var precedingEntryIndex = targetIndex - 1;
    if (precedingEntryIndex >= 0)
    {
      var keyFrameOffset = _indexEntries[precedingEntryIndex].NearestKeyFrameOffset;
      var resolvedIndex = _indexEntries.FindIndex(entry => entry.Offset == keyFrameOffset);
      if (resolvedIndex >= 0)
      {
        startEntryIndex = resolvedIndex;
      }
    }

    for (var i = startEntryIndex; i < targetIndex; i++)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var record = CaptureRecordingStorage.ReadRecord(_stream, _indexEntries[i].Offset);
      switch (record.Header.Kind)
      {
        case CaptureRecordKind.KeyFrame:
          ApplyKeyFrame(record);
          break;
        case CaptureRecordKind.FrameBatch:
          ApplyFrameBatch(record);
          break;
        case CaptureRecordKind.Event:
          break;
        default:
          throw new InvalidDataException($"Unsupported capture record kind {record.Header.Kind}.");
      }
    }

    _currentRecordIndex = targetIndex;
    _position = normalizedPosition;

    if (_options.EmitFrameAfterSeek && _compositedFrame is not null)
    {
      var sequence = precedingEntryIndex >= 0
        ? _indexEntries[precedingEntryIndex].Sequence
        : 0;
      await EmitFrame(sequence, normalizedPosition).ConfigureAwait(false);
    }
  }

  private async Task RunPlayback(CancellationToken cancellationToken)
  {
    var previousTimestamp = _position;

    while (true)
    {
      CaptureIndexEntry entry;

      await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
      try
      {
        if (_currentRecordIndex >= _indexEntries.Count)
        {
          _playbackTask = null;
          return;
        }

        entry = _indexEntries[_currentRecordIndex];
        _currentRecordIndex++;
      }
      finally
      {
        _stateLock.Release();
      }

      var recordTimestamp = TimeSpan.FromTicks(entry.TimestampTicks);
      var delay = recordTimestamp - previousTimestamp;
      if (delay > TimeSpan.Zero)
      {
        await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
      }

      var record = CaptureRecordingStorage.ReadRecord(_stream, entry.Offset);
      switch (record.Header.Kind)
      {
        case CaptureRecordKind.KeyFrame:
          ApplyKeyFrame(record);
          break;
        case CaptureRecordKind.FrameBatch:
          ApplyFrameBatch(record);
          await EmitFrame(record.Header.Sequence, recordTimestamp).ConfigureAwait(false);
          break;
        case CaptureRecordKind.Event:
          await ApplyEvent(record).ConfigureAwait(false);
          break;
        default:
          throw new InvalidDataException($"Unsupported capture record kind {record.Header.Kind}.");
      }

      previousTimestamp = recordTimestamp;

      await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
      try
      {
        _position = recordTimestamp;
      }
      finally
      {
        _stateLock.Release();
      }
    }
  }
}
