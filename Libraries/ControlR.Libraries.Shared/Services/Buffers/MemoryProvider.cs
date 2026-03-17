using Microsoft.IO;

namespace ControlR.Libraries.Shared.Services.Buffers;

public interface IMemoryProvider
{
  IEphemeralBuffer<T> CreateEphemeralBuffer<T>(int size);
  MemoryStream GetRecyclableStream();
  MemoryStream GetRecyclableStream(byte[] buffer);
}

public class MemoryProvider : IMemoryProvider
{
  private readonly RecyclableMemoryStreamManager _memoryStreamManager = new();
  public IEphemeralBuffer<T> CreateEphemeralBuffer<T>(int size)
  {
    return new EphemeralBuffer<T>(size);
  }

  public MemoryStream GetRecyclableStream()
  {
    return _memoryStreamManager.GetStream();
  }

  public MemoryStream GetRecyclableStream(byte[] buffer)
  {
    return _memoryStreamManager.GetStream(buffer);
  }
}