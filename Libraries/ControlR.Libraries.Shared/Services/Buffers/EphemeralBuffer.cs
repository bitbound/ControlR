using System.Buffers;

namespace ControlR.Libraries.Shared.Services.Buffers;

public interface IEphemeralBuffer<T> : IDisposable
{
    int Size { get; }
    T[] Value { get; }
}

internal class EphemeralBuffer<T>(int _size) : IEphemeralBuffer<T>
{
    private bool _disposedValue;

    public int Size { get; } = _size;
    public T[] Value { get; } = ArrayPool<T>.Shared.Rent(_size);

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                ArrayPool<T>.Shared.Return(Value);
            }

            _disposedValue = true;
        }
    }
}