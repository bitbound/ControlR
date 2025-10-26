
namespace ControlR.Libraries.Shared.IO;

public class CompoundReadStream : Stream
{
  private readonly bool _ownsStreams;
  private readonly List<Stream> _streams;
  private readonly long _totalLength;

  private int _currentStreamIndex = 0;

  public CompoundReadStream(bool ownsStreams, params Stream[] streams)
  {
    _ownsStreams = ownsStreams;
    _streams = [.. streams];
    if (_streams.Count == 0)
    {
      throw new ArgumentException("At least one stream must be provided", nameof(streams));
    }

    _totalLength = _streams.Sum(s => s.Length);
    if (_streams.Any(s => !s.CanRead))
    {
      throw new ArgumentException("All streams must be readable", nameof(streams));
    }
  }

  public override bool CanRead => true;

  public override bool CanSeek => false;

  public override bool CanWrite => false;

  public override long Length => _totalLength;

  public override long Position
  {
    get
    {
      return _streams.Sum(s => s.Position);
    }
    set => throw new NotSupportedException();
  }

  public override void Flush()
  {
    // No-op since this is a read-only stream
  }

  public override int Read(byte[] buffer, int offset, int count)
  {
    if (_currentStreamIndex >= _streams.Count)
    {
      return 0; // End of all streams
    }

    int bytesRead = _streams[_currentStreamIndex].Read(buffer, offset, count);
    if (bytesRead == 0)
    {
      // Move to the next stream
      _currentStreamIndex++;
      return Read(buffer, offset, count); // Recursive call to read from the next stream
    }

    return bytesRead;
  }
  public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
  {
    return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
  }

  public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
  {
    while (true)
    {
      if (_currentStreamIndex >= _streams.Count)
      {
        return 0; // End of all streams
      }

      var bytesRead = await _streams[_currentStreamIndex].ReadAsync(buffer, cancellationToken);
      if (bytesRead != 0)
      {
        return bytesRead;
      }

      // Move to the next stream
      _currentStreamIndex++;
    }
  }

  public override long Seek(long offset, SeekOrigin origin)
  {
    throw new NotSupportedException();
  }

  public override void SetLength(long value)
  {
    throw new NotSupportedException();
  }

  public override void Write(byte[] buffer, int offset, int count)
  {
    throw new NotSupportedException();
  }

  protected override void Dispose(bool disposing)
  {
    if (_ownsStreams && disposing)
    {
      foreach (var stream in _streams)
      {
        stream.Dispose();
      }
    }
    base.Dispose(disposing);
  }
}