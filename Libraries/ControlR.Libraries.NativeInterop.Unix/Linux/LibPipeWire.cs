using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.Linux;

/// <summary>
/// Minimal PipeWire P/Invoke bindings for screen capture via XDG Desktop Portal.
/// Only includes the essential functions needed for receiving video frames from portal ScreenCast streams.
/// </summary>
public static partial class LibPipeWire
{
  private const string LibraryName = "libpipewire-0.3.so.0";

  // Stream states
  public const int PW_STREAM_STATE_ERROR = -1;
  public const int PW_STREAM_STATE_UNCONNECTED = 0;
  public const int PW_STREAM_STATE_CONNECTING = 1;
  public const int PW_STREAM_STATE_PAUSED = 2;
  public const int PW_STREAM_STATE_STREAMING = 3;

  // Stream flags
  public const int PW_STREAM_FLAG_AUTOCONNECT = (1 << 0);
  public const int PW_STREAM_FLAG_INACTIVE = (1 << 1);
  public const int PW_STREAM_FLAG_MAP_BUFFERS = (1 << 2);

  // Direction
  public const int PW_DIRECTION_INPUT = 0;
  public const int PW_DIRECTION_OUTPUT = 1;

  [StructLayout(LayoutKind.Sequential)]
  public struct pw_buffer
  {
    public nint buffer;  // struct spa_buffer*
    public nint user_data;
    public ulong size;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct spa_buffer
  {
    public uint n_metas;
    public uint n_datas;
    public nint metas;  // struct spa_meta*
    public nint datas;  // struct spa_data*
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct spa_data
  {
    public uint type;
    public uint flags;
    public nint fd;
    public uint mapoffset;
    public uint maxsize;
    public nint data;
    public nint chunk;  // struct spa_chunk*
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct spa_chunk
  {
    public uint offset;
    public uint size;
    public int stride;
    public int flags;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct spa_rectangle
  {
    public uint width;
    public uint height;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct spa_fraction
  {
    public uint num;
    public uint denom;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct spa_video_info_raw
  {
    public uint format;
    public ulong modifier;
    public spa_rectangle size;
    public spa_fraction framerate;
  }

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void pw_stream_state_changed_func(nint data, int old, int state, nint error);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void pw_stream_param_changed_func(nint data, uint id, nint param);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void pw_stream_process_func(nint data);

  [StructLayout(LayoutKind.Sequential)]
  public struct pw_stream_events
  {
    public uint version;
    public nint destroy;
    public nint state_changed;
    public nint control_info;
    public nint io_changed;
    public nint param_changed;
    public nint add_buffer;
    public nint remove_buffer;
    public nint process;
    public nint drained;
    public nint command;
    public nint trigger_done;
  }

  [LibraryImport(LibraryName)]
  public static partial void pw_init(nint argc, nint argv);

  [LibraryImport(LibraryName)]
  public static partial void pw_deinit();

  [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
  public static partial nint pw_stream_new_simple(
    nint loop,
    string name,
    nint props);

  [LibraryImport(LibraryName)]
  public static partial void pw_stream_destroy(nint stream);

  [LibraryImport(LibraryName)]
  public static partial int pw_stream_connect(
    nint stream,
    int direction,
    uint target_id,
    int flags,
    nint lparams,
    uint n_params);

  [LibraryImport(LibraryName)]
  public static partial int pw_stream_disconnect(nint stream);

  [LibraryImport(LibraryName)]
  public static partial nint pw_stream_dequeue_buffer(nint stream);

  [LibraryImport(LibraryName)]
  public static partial int pw_stream_queue_buffer(nint stream, nint buffer);

  [LibraryImport(LibraryName)]
  public static partial void pw_stream_add_listener(
    nint stream,
    nint listener,
    ref pw_stream_events events,
    nint data);

  [LibraryImport(LibraryName)]
  public static partial int pw_stream_get_state(nint stream, nint error);

  [LibraryImport(LibraryName)]
  public static partial nint pw_thread_loop_new(nint name, nint props);

  [LibraryImport(LibraryName)]
  public static partial void pw_thread_loop_destroy(nint loop);

  [LibraryImport(LibraryName)]
  public static partial int pw_thread_loop_start(nint loop);

  [LibraryImport(LibraryName)]
  public static partial void pw_thread_loop_stop(nint loop);

  [LibraryImport(LibraryName)]
  public static partial nint pw_thread_loop_get_loop(nint loop);

  [LibraryImport(LibraryName)]
  public static partial void pw_thread_loop_lock(nint loop);

  [LibraryImport(LibraryName)]
  public static partial void pw_thread_loop_unlock(nint loop);

  [LibraryImport(LibraryName)]
  public static partial nint pw_core_new(nint context, nint properties, uint user_data_size);

  [LibraryImport(LibraryName)]
  public static partial void pw_core_disconnect(nint core);

  [LibraryImport(LibraryName)]
  public static partial nint pw_context_new(nint loop, nint properties, uint user_data_size);

  [LibraryImport(LibraryName)]
  public static partial void pw_context_destroy(nint context);

  [LibraryImport(LibraryName)]
  public static partial nint pw_context_connect(nint context, nint properties, uint user_data_size);

  [LibraryImport(LibraryName)]
  public static partial nint pw_context_connect_fd(nint context, int fd, nint properties, uint user_data_size);
}
