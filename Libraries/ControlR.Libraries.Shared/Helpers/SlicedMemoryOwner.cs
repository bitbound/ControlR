using System.Buffers;

namespace ControlR.Libraries.Shared.Helpers;

public sealed class SlicedMemoryOwner<T>(IMemoryOwner<T> internalOwner, int length) : IMemoryOwner<T>
{
    private readonly IMemoryOwner<T> _internalOwner = internalOwner;
    private readonly int _length = length;

    public Memory<T> Memory => _internalOwner.Memory[.._length];

    public void Dispose()
    {
        _internalOwner.Dispose();
    }
}
