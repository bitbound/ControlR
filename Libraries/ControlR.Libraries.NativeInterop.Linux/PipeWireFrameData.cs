using System;
using System.Buffers;

namespace ControlR.Libraries.NativeInterop.Linux;

public sealed class PipeWireFrameData : IDisposable
{
  private bool _disposed;

  /// <summary>
  /// Read-only view of the data slice (already sized to the actual frame data length).
  /// </summary>
  public ReadOnlyMemory<byte> Data => DataOwner.Memory;
  /// <summary>
  /// Pooled owner containing the frame bytes. The owner MUST be disposed to return the buffer to the pool.
  /// </summary>
  public required IMemoryOwner<byte> DataOwner { get; init; }
  public required int Height { get; init; }
  public int Length => Data.Length;
  public required uint PixelFormat { get; init; }
  public required int Stride { get; init; }
  public required int Width { get; init; }

  public void Dispose()
  {
    if (!_disposed)
    {
      try { DataOwner?.Dispose(); } catch { }
      _disposed = true;
    }
    GC.SuppressFinalize(this);
  }
}
