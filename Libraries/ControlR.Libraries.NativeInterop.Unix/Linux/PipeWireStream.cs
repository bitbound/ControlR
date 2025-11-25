using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.Linux;

public class PipeWireFrameData
{
  public required byte[] Data { get; init; }
  public required int Height { get; init; }
  public required uint PixelFormat { get; init; }
  public required int Stride { get; init; }
  public required int Width { get; init; }
}

public class PipeWireStream : IDisposable
{
  public const uint SPA_VIDEO_FORMAT_BGRA = 12;

  private readonly int _height;

  private readonly ILogger _logger;
  private readonly uint _nodeId;
  private readonly int _pipewireFd;
  private readonly int _width;

  private nint _appsink;
  private Thread? _captureThread;

  private bool _disposed;
  private PipeWireFrameData? _latestFrame;
  private nint _pipeline;

  public PipeWireStream(ILogger logger, uint nodeId, SafeHandle pipewireFd, int expectedLogicalWidth, int expectedLogicalHeight)
  {
    if (expectedLogicalWidth <= 0 || expectedLogicalHeight <= 0)
    {
      throw new ArgumentException("Stream dimensions must be provided and positive.");
    }

    _logger = logger;
    _nodeId = nodeId;
    _pipewireFd = (int)pipewireFd.DangerousGetHandle();
    _width = expectedLogicalWidth;
    _height = expectedLogicalHeight;

    InitializeGStreamer();
  }


  public int ActualHeight => _height;

  public int ActualWidth => _width;

  public bool IsStreaming => !_disposed && _pipeline != nint.Zero;
  public double ScaleFactor => 1.0;

  public void Dispose()
  {
    if (!_disposed)
    {
      Cleanup();
      _disposed = true;
    }
  }

  public PipeWireFrameData GetLatestFrame()
  {
    return Volatile.Read(ref _latestFrame) 
      ?? throw new InvalidOperationException("No frame available yet.");
  }

  public bool TryGetLatestFrame([NotNullWhen(true)] out PipeWireFrameData? frame)
  {
    frame = Volatile.Read(ref _latestFrame);
    return frame is not null;
  }

  private void Cleanup()
  {
    try
    {
      if (_pipeline != nint.Zero)
      {
        GStreamer.gst_element_set_state(_pipeline, (int)GStreamer.State.Null);
        GStreamer.gst_object_unref(_pipeline);
        _pipeline = nint.Zero;
      }

      if (_appsink != nint.Zero)
      {
        GStreamer.gst_object_unref(_appsink);
        _appsink = nint.Zero;
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error cleaning up");
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
        $"videoconvert ! videoscale ! " + 
        $"video/x-raw,format=BGRA,width={_width},height={_height} ! " + 
        $"appsink name=sink max-buffers=1";

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
    try
    {
      while (!_disposed)
      {
        var sample = GStreamer.gst_app_sink_try_pull_sample(_appsink, 100 * 1000000);

        if (sample == nint.Zero)
        {
          if (GStreamer.gst_app_sink_is_eos(_appsink)) break;
          continue;
        }

        try
        {
          var buffer = GStreamer.gst_sample_get_buffer(sample);
          if (buffer != nint.Zero)
          {
            if (GStreamer.gst_buffer_map(buffer, out var mapInfo, GStreamer.GST_MAP_READ))
            {
              try
              {
                var size = (int)mapInfo.size;
                var data = new byte[size];
                Marshal.Copy(mapInfo.data, data, 0, size);

                Volatile.Write(ref _latestFrame, new PipeWireFrameData
                {
                  Data = data,
                  Width = _width,
                  Height = _height,
                  Stride = _width * 4,
                  PixelFormat = SPA_VIDEO_FORMAT_BGRA
                });
              }
              finally
              {
                GStreamer.gst_buffer_unmap(buffer, ref mapInfo);
              }
            }
          }
        }
        finally
        {
          GStreamer.gst_sample_unref(sample);
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error in capture loop");
    }
  }
}
