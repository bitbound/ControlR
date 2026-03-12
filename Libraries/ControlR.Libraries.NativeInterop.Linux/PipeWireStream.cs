using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Buffers;
using ControlR.Libraries.Shared.Services.Buffers;

namespace ControlR.Libraries.NativeInterop.Linux;

public class PipeWireStream : IDisposable
{
  public const uint SPA_VIDEO_FORMAT_BGRA = 12;

  private const ulong AppSinkPullTimeout = 100 * 1_000_000;

  private readonly DateTime _createdUtc = DateTime.UtcNow;
  private readonly TaskCompletionSource<bool> _firstFrameReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private readonly ILogger _logger;
  private readonly int _logicalHeight;
  private readonly int _logicalWidth;
  private readonly uint _nodeId;
  private readonly int _pipewireFd;

  private nint _appsink;
  private Thread? _captureThread;
  private bool _disposed;
  private long _lastFrameReceivedUtcTicks;
  private PipeWireFrameData? _latestFrame;
  private int _loggedDimensionMismatch;
  private int _loggedStrideMismatch;
  private int _physicalHeight;
  private int _physicalWidth;
  private nint _pipeline;

  public PipeWireStream(
    uint nodeId,
    SafeHandle pipewireFd,
    int expectedLogicalWidth,
    int expectedLogicalHeight,
    ILogger<PipeWireStream> logger)
  {
    if (expectedLogicalWidth <= 0 || expectedLogicalHeight <= 0)
    {
      throw new ArgumentException("Stream dimensions must be provided and positive.");
    }

    _logger = logger;
    _nodeId = nodeId;
    _pipewireFd = (int)pipewireFd.DangerousGetHandle();
    _logicalWidth = expectedLogicalWidth;
    _logicalHeight = expectedLogicalHeight;

    InitializeGStreamer();
  }

  /// <summary>
  /// Gets the latest known physical frame height in pixels.
  /// Returns 0 until the first frame has been negotiated and received.
  /// </summary>
  public DateTime CreatedUtc => _createdUtc;
  public int Height => Volatile.Read(ref _physicalHeight);
  public bool IsStreaming => !_disposed && _pipeline != nint.Zero;
  public DateTime? LastFrameReceivedUtc
  {
    get
    {
      var ticks = Interlocked.Read(ref _lastFrameReceivedUtcTicks);
      return ticks > 0
        ? new DateTime(ticks, DateTimeKind.Utc)
        : null;
    }
  }

  /// <summary>
  /// Gets the latest known physical frame width in pixels.
  /// Returns 0 until the first frame has been negotiated and received.
  /// </summary>
  public int Width => Volatile.Read(ref _physicalWidth);

  public void Dispose()
  {
    if (!_disposed)
    {
      Cleanup();
      _disposed = true;
    }
    GC.SuppressFinalize(this);
  }

  public PipeWireFrameData GetLatestFrame()
  {
    var current = Volatile.Read(ref _latestFrame)
      ?? throw new InvalidOperationException("No frame available yet.");

    return CloneFrameData(current);
  }

  public bool TryGetLatestFrame([NotNullWhen(true)] out PipeWireFrameData? frame)
  {
    try
    {
      var current = Volatile.Read(ref _latestFrame);
      if (current is null)
      {
        frame = null;
        return false;
      }
      frame = CloneFrameData(current);
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error trying to get latest frame.");
      frame = null;
      return false;
    }
  }

  /// <summary>
  /// Waits until at least one frame has been received and physical frame dimensions are known.
  /// </summary>
  /// <remarks>
  /// Logical dimensions are provided by portal metadata. Physical dimensions are negotiated from stream caps.
  /// This method ensures callers can await negotiated physical dimensions explicitly instead of polling.
  /// </remarks>
  public async Task<bool> WaitForFirstFrame(CancellationToken cancellationToken)
  {
    if (Volatile.Read(ref _physicalWidth) > 0 && Volatile.Read(ref _physicalHeight) > 0)
    {
      return true;
    }

    var result = await _firstFrameReceived.Task.WaitAsync(cancellationToken);
    return result && Volatile.Read(ref _physicalWidth) > 0 && Volatile.Read(ref _physicalHeight) > 0;
  }

  private void Cleanup()
  {
    try
    {
      if (_pipeline != nint.Zero)
      {
        _ = GStreamer.gst_element_set_state(_pipeline, (int)GStreamer.State.Null);
        GStreamer.gst_object_unref(_pipeline);
        _pipeline = nint.Zero;
      }

      if (_appsink != nint.Zero)
      {
        GStreamer.gst_object_unref(_appsink);
        _appsink = nint.Zero;
      }

      using var old = Interlocked.Exchange(ref _latestFrame, null);
      _firstFrameReceived.TrySetResult(false);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error cleaning up");
    }
  }

  private PipeWireFrameData CloneFrameData(PipeWireFrameData source)
  {
    var size = source.Data.Length;
    var array = ArrayPool<byte>.Shared.Rent(size);
    try
    {
      source.Data.Span.CopyTo(array.AsSpan(0, size));
      var arrayOwner = new ArrayPoolOwner(array, size);

      return new PipeWireFrameData
      {
        DataOwner = arrayOwner,
        Width = source.Width,
        Height = source.Height,
        Stride = source.Stride,
        PixelFormat = source.PixelFormat
      };
    }
    catch
    {
      ArrayPool<byte>.Shared.Return(array);
      throw;
    }
  }

  private void InitializeGStreamer()
  {
    try
    {
      int argc = 0;
      nint argv = nint.Zero;
      GStreamer.gst_init(ref argc, ref argv);

      // We use 'appsink' to get the buffers directly in the process memory.
      // max-buffers=1 limits the queue to a single buffer.
      var pipelineStr =
        $"pipewiresrc fd={_pipewireFd} path={_nodeId} always-copy=true ! " +
        $"appsink name=sink max-buffers=1 drop=true sync=false caps=video/x-raw,format=BGRA";

      _logger.LogInformation("Initializing GStreamer pipeline: {Pipeline}", pipelineStr);

      _pipeline = GStreamer.gst_parse_launch(pipelineStr, out var error);
      if (error != nint.Zero)
      {
        throw new InvalidOperationException($"Failed to parse pipeline. Error pointer: {error}");
      }

      _appsink = GStreamer.gst_bin_get_by_name(_pipeline, "sink");
      if (_appsink == nint.Zero)
      {
        throw new InvalidOperationException("Failed to get appsink element.");
      }

      var ret = GStreamer.gst_element_set_state(_pipeline, (int)GStreamer.State.Playing);
      if (ret == 0)
      {
        throw new InvalidOperationException("Failed to set pipeline to playing state.");
      }

      _captureThread = new Thread(StartReadingGstreamerOutput) { IsBackground = true };
      _captureThread.Start();
    }
    catch (DllNotFoundException ex)
    {
      var message = "GStreamer libraries not found. Please install them with: sudo apt install libgstreamer1.0-0 gstreamer1.0-plugins-base gstreamer1.0-plugins-good";
      _logger.LogError(ex, "{Message}", message);
      Cleanup();
      throw new InvalidOperationException(message, ex);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to initialize GStreamer");
      Cleanup();
      throw;
    }
  }

  private void StartReadingGstreamerOutput()
  {

    while (!Volatile.Read(ref _disposed))
    {
      try
      {
        var sample = GStreamer.gst_app_sink_try_pull_sample(_appsink, AppSinkPullTimeout);

        if (sample == nint.Zero)
        {
          if (GStreamer.gst_app_sink_is_eos(_appsink)) break;
          continue;
        }

        using var sampleDisposer = new CallbackDisposable(() =>
        {
          GStreamer.gst_sample_unref(sample);
        });

        // Start with portal-reported logical size. If caps provide concrete frame size,
        // those values become the physical dimensions for capture.
        var physicalWidth = _logicalWidth;
        var physicalHeight = _logicalHeight;

        var caps = GStreamer.gst_sample_get_caps(sample);
        if (caps != nint.Zero)
        {
          var structure = GStreamer.gst_caps_get_structure(caps, 0);
          if (structure != nint.Zero)
          {
            if (GStreamer.gst_structure_get_int(structure, "width", out var capWidth) != 0 && capWidth > 0)
            {
              physicalWidth = capWidth;
            }
            if (GStreamer.gst_structure_get_int(structure, "height", out var capHeight) != 0 && capHeight > 0)
            {
              physicalHeight = capHeight;
            }
          }
        }

        var buffer = GStreamer.gst_sample_get_buffer(sample);
        if (buffer == nint.Zero)
        {
          continue;
        }

        if (!GStreamer.gst_buffer_map(buffer, out var mapInfo, GStreamer.GST_MAP_READ))
        {
          continue;
        }

        using var mapDisposer = new CallbackDisposable(() =>
        {
          GStreamer.gst_buffer_unmap(buffer, ref mapInfo);
        });

        var size = checked((int)mapInfo.size);
        var array = ArrayPool<byte>.Shared.Rent(size);
        try
        {
          unsafe
          {
            fixed (byte* destPtr = array)
            {
              Buffer.MemoryCopy((void*)mapInfo.data, destPtr, array.Length, size);
            }
          }

          if ((uint)physicalWidth != (uint)_logicalWidth || (uint)physicalHeight != (uint)_logicalHeight)
          {
            if (Interlocked.CompareExchange(ref _loggedDimensionMismatch, 1, 0) == 0)
            {
              _logger.LogInformation(
                "PipeWire stream caps size ({Width}x{Height} physical px) differs from portal-reported logical size ({ExpectedLogicalWidth}x{ExpectedLogicalHeight}).",
                physicalWidth,
                physicalHeight,
                _logicalWidth,
                _logicalHeight);
            }
          }

          Volatile.Write(ref _physicalWidth, physicalWidth);
          Volatile.Write(ref _physicalHeight, physicalHeight);
          Interlocked.Exchange(ref _lastFrameReceivedUtcTicks, DateTime.UtcNow.Ticks);
          _firstFrameReceived.TrySetResult(true);

          var stride = physicalWidth * 4;
          if (physicalHeight > 0)
          {
            var computedStride = size / physicalHeight;
            if (computedStride >= physicalWidth * 4)
            {
              stride = computedStride;
            }
            else if (Interlocked.CompareExchange(ref _loggedStrideMismatch, 1, 0) == 0)
            {
              _logger.LogWarning(
                "PipeWire frame stride computed as {Stride} which is smaller than width*4 ({MinStride}). Falling back to width*4.",
                computedStride,
                physicalWidth * 4);
            }
          }

          var arrayOwner = new ArrayPoolOwner(array, size);
          var newFrame = new PipeWireFrameData
          {
            DataOwner = arrayOwner,
            Width = physicalWidth,
            Height = physicalHeight,
            Stride = stride,
            PixelFormat = SPA_VIDEO_FORMAT_BGRA
          };

          Interlocked.Exchange(ref _latestFrame, newFrame)?.Dispose();
        }
        catch
        {
          ArrayPool<byte>.Shared.Return(array);
          throw;
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in capture loop iteration");
      }
    }

    _logger.LogInformation("GStreamer reading ended.");
  }
}