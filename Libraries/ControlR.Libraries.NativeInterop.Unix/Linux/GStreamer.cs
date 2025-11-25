using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.Linux;

public static class GStreamer
{
    private const string LibraryName = "libgstreamer-1.0.so.0";
    private const string AppLibraryName = "libgstapp-1.0.so.0";

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gst_init(ref int argc, ref nint argv);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint gst_parse_launch(string pipeline_description, out nint error);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int gst_element_set_state(nint element, int state);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint gst_bin_get_by_name(nint bin, string name);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gst_object_unref(nint obj);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gst_sample_unref(nint sample);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint gst_sample_get_buffer(nint sample);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint gst_sample_get_caps(nint sample);
    
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint gst_caps_get_structure(nint caps, uint index);
    
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int gst_structure_get_int(nint structure, string fieldname, out int value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool gst_buffer_map(nint buffer, out GstMapInfo info, int flags);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void gst_buffer_unmap(nint buffer, ref GstMapInfo info);

    [DllImport(AppLibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint gst_app_sink_pull_sample(nint appsink);
    
    [DllImport(AppLibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint gst_app_sink_try_pull_sample(nint appsink, ulong timeout);

    [DllImport(AppLibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool gst_app_sink_is_eos(nint appsink);

    [StructLayout(LayoutKind.Sequential)]
    public struct GstMapInfo
    {
        public nint memory;
        public int flags;
        public nint data;
        public nint size;
        public nint maxsize;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public nint[] user_data;
        public nint _gst_reserved1;
        public nint _gst_reserved2;
        public nint _gst_reserved3;
        public nint _gst_reserved4;
    }

    public enum State
    {
        VoidPending = 0,
        Null = 1,
        Ready = 2,
        Paused = 3,
        Playing = 4
    }

    public const int GST_MAP_READ = 1;
}
