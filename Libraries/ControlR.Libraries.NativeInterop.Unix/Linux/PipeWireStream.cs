using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.Linux;

/// <summary>
/// Represents a captured video frame from PipeWire with format metadata.
/// </summary>
public class PipeWireFrameData
{
  public required byte[] Data { get; init; }
  public required int Width { get; init; }
  public required int Height { get; init; }
  public required int Stride { get; init; }
  public required uint PixelFormat { get; init; }
}

/// <summary>
/// PipeWire stream wrapper for capturing video frames from XDG Desktop Portal ScreenCast.
/// Manages PipeWire connection, stream lifecycle, and frame extraction.
/// </summary>
public class PipeWireStream : IDisposable
{
  private readonly ILogger _logger;
  private readonly uint _nodeId;
  private nint _threadLoop;
  private nint _context;
  private nint _core;
  private nint _stream;
  private nint _listener;
  private LibPipeWire.pw_stream_events _events;
  private GCHandle _eventsHandle;
  private GCHandle _thisHandle;
  private bool _disposed;
  private bool _isStreaming;
  private PipeWireFrameData? _lastFrame;
  private readonly object _frameLock = new();
  private int _videoWidth;
  private int _videoHeight;
  private uint _videoFormat;

  public PipeWireStream(ILogger logger, uint nodeId, SafeHandle pipewireFd)
  {
    _logger = logger;
    _nodeId = nodeId;

    try
    {
      LibPipeWire.pw_init(nint.Zero, nint.Zero);

      _threadLoop = LibPipeWire.pw_thread_loop_new(nint.Zero, nint.Zero);
      if (_threadLoop == nint.Zero)
      {
        throw new InvalidOperationException("Failed to create PipeWire thread loop");
      }

      var loop = LibPipeWire.pw_thread_loop_get_loop(_threadLoop);

      _context = LibPipeWire.pw_context_new(loop, nint.Zero, 0);
      if (_context == nint.Zero)
      {
        throw new InvalidOperationException("Failed to create PipeWire context");
      }

      _core = LibPipeWire.pw_context_connect_fd(_context, (int)pipewireFd.DangerousGetHandle(), nint.Zero, 0);
      if (_core == nint.Zero)
      {
        throw new InvalidOperationException("Failed to connect to PipeWire remote");
      }

      _stream = LibPipeWire.pw_stream_new_simple(loop, "ControlR ScreenCast", nint.Zero);
      if (_stream == nint.Zero)
      {
        throw new InvalidOperationException("Failed to create PipeWire stream");
      }

      _thisHandle = GCHandle.Alloc(this);
      _events = new LibPipeWire.pw_stream_events
      {
        version = 0,
        state_changed = Marshal.GetFunctionPointerForDelegate(new LibPipeWire.pw_stream_state_changed_func(OnStateChanged)),
        param_changed = Marshal.GetFunctionPointerForDelegate(new LibPipeWire.pw_stream_param_changed_func(OnParamChanged)),
        process = Marshal.GetFunctionPointerForDelegate(new LibPipeWire.pw_stream_process_func(OnProcess))
      };
      _eventsHandle = GCHandle.Alloc(_events, GCHandleType.Pinned);

      _listener = Marshal.AllocHGlobal(256);
      LibPipeWire.pw_stream_add_listener(_stream, _listener, ref _events, GCHandle.ToIntPtr(_thisHandle));

      var connectResult = LibPipeWire.pw_stream_connect(
        _stream,
        LibPipeWire.PW_DIRECTION_INPUT,
        _nodeId,
        LibPipeWire.PW_STREAM_FLAG_AUTOCONNECT | LibPipeWire.PW_STREAM_FLAG_MAP_BUFFERS,
        nint.Zero,
        0);

      if (connectResult < 0)
      {
        throw new InvalidOperationException($"Failed to connect stream: {connectResult}");
      }

      if (LibPipeWire.pw_thread_loop_start(_threadLoop) < 0)
      {
        throw new InvalidOperationException("Failed to start PipeWire thread loop");
      }

      _logger.LogInformation("PipeWire stream created for node {NodeId}", nodeId);
    }
    catch
    {
      Cleanup();
      throw;
    }
  }

  public PipeWireFrameData? GetLatestFrame()
  {
    lock (_frameLock)
    {
      if (_lastFrame is null)
      {
        return null;
      }

      return new PipeWireFrameData
      {
        Data = (byte[])_lastFrame.Data.Clone(),
        Width = _lastFrame.Width,
        Height = _lastFrame.Height,
        Stride = _lastFrame.Stride,
        PixelFormat = _lastFrame.PixelFormat
      };
    }
  }

  public bool IsStreaming => _isStreaming;

  public void Dispose()
  {
    if (_disposed)
    {
      return;
    }

    Cleanup();
    _disposed = true;
  }

  private void Cleanup()
  {
    try
    {
      if (_threadLoop != nint.Zero)
      {
        LibPipeWire.pw_thread_loop_stop(_threadLoop);
      }

      if (_stream != nint.Zero)
      {
        LibPipeWire.pw_stream_disconnect(_stream);
        LibPipeWire.pw_stream_destroy(_stream);
        _stream = nint.Zero;
      }

      if (_listener != nint.Zero)
      {
        Marshal.FreeHGlobal(_listener);
        _listener = nint.Zero;
      }

      if (_eventsHandle.IsAllocated)
      {
        _eventsHandle.Free();
      }

      if (_thisHandle.IsAllocated)
      {
        _thisHandle.Free();
      }

      if (_core != nint.Zero)
      {
        LibPipeWire.pw_core_disconnect(_core);
        _core = nint.Zero;
      }

      if (_context != nint.Zero)
      {
        LibPipeWire.pw_context_destroy(_context);
        _context = nint.Zero;
      }

      if (_threadLoop != nint.Zero)
      {
        LibPipeWire.pw_thread_loop_destroy(_threadLoop);
        _threadLoop = nint.Zero;
      }

      lock (_frameLock)
      {
        _lastFrame = null;
      }

      LibPipeWire.pw_deinit();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error cleaning up PipeWire stream");
    }
  }

  private static void OnStateChanged(nint data, int old, int state, nint error)
  {
    try
    {
      var handle = GCHandle.FromIntPtr(data);
      if (handle.Target is PipeWireStream stream)
      {
        stream._isStreaming = state == LibPipeWire.PW_STREAM_STATE_STREAMING;
        stream._logger.LogInformation("PipeWire stream state changed: {OldState} -> {NewState}", old, state);

        if (state == LibPipeWire.PW_STREAM_STATE_ERROR)
        {
          stream._logger.LogError("PipeWire stream entered error state");
        }
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error in OnStateChanged: {ex}");
    }
  }

  private static void OnParamChanged(nint data, uint id, nint param)
  {
    try
    {
      var handle = GCHandle.FromIntPtr(data);
      if (handle.Target is PipeWireStream stream && param != nint.Zero)
      {
        stream.ParseStreamParameters(param);
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error in OnParamChanged: {ex}");
    }
  }

  private static void OnProcess(nint data)
  {
    try
    {
      var handle = GCHandle.FromIntPtr(data);
      if (handle.Target is PipeWireStream stream)
      {
        stream.ProcessFrame();
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error in OnProcess: {ex}");
    }
  }

  private void ParseStreamParameters(nint param)
  {
    try
    {
      unsafe
      {
        var paramPtr = (byte*)param;
        var size = *(uint*)(paramPtr + 0);
        var type = *(uint*)(paramPtr + 4);

        const uint SPA_PARAM_Format = 4;
        if (type != SPA_PARAM_Format)
        {
          return;
        }

        var dataPtr = paramPtr + 16;
        var remaining = (int)size - 16;

        while (remaining >= 8)
        {
          var key = *(uint*)dataPtr;
          var valueType = *(uint*)(dataPtr + 4);
          dataPtr += 8;
          remaining -= 8;

          const uint SPA_FORMAT_VIDEO_size = 0x30002;
          const uint SPA_FORMAT_VIDEO_format = 0x30001;

          if (key == SPA_FORMAT_VIDEO_size && remaining >= 8)
          {
            _videoWidth = *(int*)dataPtr;
            _videoHeight = *(int*)(dataPtr + 4);
            dataPtr += 8;
            remaining -= 8;
            _logger.LogInformation("Video dimensions: {Width}x{Height}", _videoWidth, _videoHeight);
          }
          else if (key == SPA_FORMAT_VIDEO_format && remaining >= 4)
          {
            _videoFormat = *(uint*)dataPtr;
            dataPtr += 4;
            remaining -= 4;
            _logger.LogInformation("Video format: {Format}", _videoFormat);
          }
          else
          {
            break;
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error parsing stream parameters");
    }
  }

  private void ProcessFrame()
  {
    try
    {
      var bufferPtr = LibPipeWire.pw_stream_dequeue_buffer(_stream);
      if (bufferPtr == nint.Zero)
      {
        return;
      }

      try
      {
        var buffer = Marshal.PtrToStructure<LibPipeWire.pw_buffer>(bufferPtr);
        if (buffer.buffer == nint.Zero)
        {
          return;
        }

        var spaBuffer = Marshal.PtrToStructure<LibPipeWire.spa_buffer>(buffer.buffer);
        if (spaBuffer.n_datas == 0 || spaBuffer.datas == nint.Zero)
        {
          return;
        }

        var spaData = Marshal.PtrToStructure<LibPipeWire.spa_data>(spaBuffer.datas);
        if (spaData.data == nint.Zero || spaData.chunk == nint.Zero)
        {
          return;
        }

        var chunk = Marshal.PtrToStructure<LibPipeWire.spa_chunk>(spaData.chunk);

        var width = _videoWidth > 0 ? _videoWidth : chunk.stride / 4;
        var height = _videoHeight > 0 ? _videoHeight : (int)chunk.size / chunk.stride;
        var stride = chunk.stride;

        if (width <= 0 || height <= 0 || width > 10000 || height > 10000)
        {
          _logger.LogWarning("Invalid frame dimensions: {Width}x{Height}", width, height);
          return;
        }

        var dataPtr = spaData.data + (int)chunk.offset;
        var dataSize = Math.Min((int)chunk.size, stride * height);

        var frameData = new byte[dataSize];
        Marshal.Copy(dataPtr, frameData, 0, dataSize);

        lock (_frameLock)
        {
          _lastFrame = new PipeWireFrameData
          {
            Data = frameData,
            Width = width,
            Height = height,
            Stride = stride,
            PixelFormat = _videoFormat
          };
        }
      }
      finally
      {
        LibPipeWire.pw_stream_queue_buffer(_stream, bufferPtr);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing PipeWire frame");
    }
  }
}
