namespace ControlR.Libraries.Shared.Services.Buffers;

public interface IMemoryProvider
{
    IEphemeralBuffer<T> CreateEphemeralBuffer<T>(int size);
}

public class MemoryProvider : IMemoryProvider
{
    public IEphemeralBuffer<T> CreateEphemeralBuffer<T>(int size)
    {
        return new EphemeralBuffer<T>(size);
    }
}