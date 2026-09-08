using System.Buffers;

namespace ControlR.Libraries.Shared.Services.Buffers;

/// <summary>
/// Wraps a rented array from <see cref="ArrayPool{T}"/> and returns it to the pool on dispose.
/// Provides a correctly-sized <see cref="Memory{T}"/> view over the rented array.
/// </summary>
public sealed class ArrayPoolOwner : IMemoryOwner<byte>
{
  private readonly int _length;

  private byte[]? _array;

  public ArrayPoolOwner(byte[] rentedArray, int length)
  {
    if (rentedArray is null) throw new ArgumentNullException(nameof(rentedArray));
    if (length < 0 || length > rentedArray.Length) throw new ArgumentOutOfRangeException(nameof(length));

    _array = rentedArray;
    _length = length;
  }

  public Memory<byte> Memory => _array is not null ? _array.AsMemory(0, _length) : Memory<byte>.Empty;

  public void Dispose()
  {
    if (_array is not null)
    {
      ArrayPool<byte>.Shared.Return(_array);
      _array = null;
    }
    GC.SuppressFinalize(this);
  }
}
