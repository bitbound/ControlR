using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace ControlR.Libraries.Shared.IO;

public class RedirectableStream : Stream
{
    private readonly ConcurrentQueue<byte[]> _queue = new();
    private readonly AutoResetEvent _writeSignal = new(false);
    private long _length;
    private long _position;

    public override bool CanRead => false;

    public override bool CanSeek => true;

    public override bool CanWrite => true;

    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => _position = value;
    }

    public override void Flush()
    {

    }

    public async IAsyncEnumerable<byte[]> GetRedirectedStream([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Run(_writeSignal.WaitOne, cancellationToken);
            while (_queue.TryDequeue(out var result))
            {
                yield return result;
            }

        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
                _position = offset;
                break;
            case SeekOrigin.Current:
                _position += offset;
                break;
            case SeekOrigin.End:
                _position = _length - offset;
                break;
            default:
                break;
        }
        return _position;
    }

    public override void SetLength(long value)
    {
        _length = value;
    }
    public override void Write(byte[] buffer, int offset, int count)
    {
        var bytesToWrite = buffer
            .Skip(offset)
            .Take(count)
            .ToArray();

        _queue.Enqueue(bytesToWrite);
        _position += bytesToWrite.Length;
        if (_position >= _length)
        {
            _length = _position + 1;
        }
        _writeSignal.Set();
    }
}
