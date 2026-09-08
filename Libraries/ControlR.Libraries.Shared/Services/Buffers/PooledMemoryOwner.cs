using System.Buffers;

namespace ControlR.Libraries.Shared.Services.Buffers;

/// <summary>
/// A small wrapper around an <see cref="IMemoryOwner{Byte}"/> rented from a <see cref="MemoryPool{Byte}"/>
/// that exposes a correctly-sized view over the rented memory and returns it to the pool on dispose.
/// </summary>
public sealed class PooledMemoryOwner : IMemoryOwner<byte>
{
  private readonly IMemoryOwner<byte> _inner;
  private readonly int _length;

  public PooledMemoryOwner(IMemoryOwner<byte> inner, int length)
  {
    _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    if (length < 0 || length > inner.Memory.Length) throw new ArgumentOutOfRangeException(nameof(length));
    _length = length;
  }

  public Memory<byte> Memory => _inner.Memory[.._length];

  /// <summary>
  /// Rent a pooled owner sized for <paramref name="size"/>.
  /// </summary>
  public static PooledMemoryOwner Rent(int size)
  {
    var inner = MemoryPool<byte>.Shared.Rent(size);
    return new PooledMemoryOwner(inner, size);
  }

  public void Dispose()
  {
    _inner.Dispose();
    GC.SuppressFinalize(this);
  }
}
