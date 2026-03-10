namespace ControlR.Libraries.TestingUtilities.FileSystem;

internal sealed class FakeFileSystemStream : Stream
{
  private readonly Action<byte[]> _commit;
  private readonly MemoryStream _inner;
  private readonly bool _isReadable;
  private readonly bool _isWritable;
  private readonly Action _release;

  private bool _disposed;

  public FakeFileSystemStream(
    byte[] content,
    bool append,
    bool isReadable,
    bool isWritable,
    Action<byte[]> commit,
    Action release)
  {
    _commit = commit;
    _inner = new MemoryStream();
    _isReadable = isReadable;
    _isWritable = isWritable;
    _release = release;

    if (content.Length > 0)
    {
      _inner.Write(content, 0, content.Length);
    }

    if (append)
    {
      _inner.Position = _inner.Length;
    }
    else
    {
      _inner.Position = 0;
    }
  }

  public override bool CanRead => !_disposed && _isReadable;
  public override bool CanSeek => !_disposed;
  public override bool CanWrite => !_disposed && _isWritable;
  public override long Length
  {
    get
    {
      ThrowIfDisposed();
      return _inner.Length;
    }
  }
  public override long Position
  {
    get
    {
      ThrowIfDisposed();
      return _inner.Position;
    }
    set
    {
      ThrowIfDisposed();
      _inner.Position = value;
    }
  }

  public override async ValueTask DisposeAsync()
  {
    if (_disposed)
    {
      return;
    }

    _disposed = true;
    Commit();
    await _inner.DisposeAsync();
    _release();
  }

  public override void Flush()
  {
    ThrowIfDisposed();
    _inner.Flush();
    Commit();
  }

  public override Task FlushAsync(CancellationToken cancellationToken)
  {
    ThrowIfDisposed();
    cancellationToken.ThrowIfCancellationRequested();
    Commit();
    return Task.CompletedTask;
  }

  public override int Read(byte[] buffer, int offset, int count)
  {
    ThrowIfDisposed();
    ThrowIfWriteOnly();
    return _inner.Read(buffer, offset, count);
  }

  public override int Read(Span<byte> buffer)
  {
    ThrowIfDisposed();
    ThrowIfWriteOnly();
    return _inner.Read(buffer);
  }

  public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
  {
    ThrowIfDisposed();
    ThrowIfWriteOnly();
    return _inner.ReadAsync(buffer, cancellationToken);
  }

  public override long Seek(long offset, SeekOrigin origin)
  {
    ThrowIfDisposed();
    return _inner.Seek(offset, origin);
  }

  public override void SetLength(long value)
  {
    ThrowIfDisposed();
    ThrowIfReadOnly();
    _inner.SetLength(value);
  }

  public override void Write(byte[] buffer, int offset, int count)
  {
    ThrowIfDisposed();
    ThrowIfReadOnly();
    _inner.Write(buffer, offset, count);
  }

  public override void Write(ReadOnlySpan<byte> buffer)
  {
    ThrowIfDisposed();
    ThrowIfReadOnly();
    _inner.Write(buffer);
  }

  public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
  {
    ThrowIfDisposed();
    ThrowIfReadOnly();
    return _inner.WriteAsync(buffer, cancellationToken);
  }

  protected override void Dispose(bool disposing)
  {
    if (_disposed)
    {
      return;
    }

    _disposed = true;
    if (disposing)
    {
      Commit();
      _inner.Dispose();
      _release();
    }
  }

  private void Commit()
  {
    if (!_isWritable)
    {
      return;
    }

    _commit(_inner.ToArray());
  }

  private void ThrowIfDisposed()
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
  }

  private void ThrowIfReadOnly()
  {
    if (!_isWritable)
    {
      throw new NotSupportedException("Stream does not support writing.");
    }
  }

  private void ThrowIfWriteOnly()
  {
    if (!_isReadable)
    {
      throw new NotSupportedException("Stream does not support reading.");
    }
  }
}
