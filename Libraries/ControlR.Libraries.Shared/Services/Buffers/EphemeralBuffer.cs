using System.Buffers;

namespace ControlR.Libraries.Shared.Services.Buffers;

public interface IEphemeralBuffer<T> : IDisposable
{
  int Size { get; }
  T[] Value { get; }
}

internal sealed class EphemeralBuffer<T>(int size) : IEphemeralBuffer<T>
{
  private bool _disposedValue;

  public int Size { get; } = size;
  public T[] Value { get; } = ArrayPool<T>.Shared.Rent(size);

  public void Dispose()
  {
    Dispose(true);
  }

  private void Dispose(bool disposing)
  {
    if (_disposedValue)
    {
      return;
    }

    _disposedValue = true;

    if (disposing)
    {
      ArrayPool<T>.Shared.Return(Value);
    }
  }
}