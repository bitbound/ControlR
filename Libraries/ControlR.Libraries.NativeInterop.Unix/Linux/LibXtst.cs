using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.Linux;

public static class LibXtst
{
    private const string LibraryName = "libXtst.so.6";

    
    [DllImport(LibraryName)]
    public static extern void XTestFakeButtonEvent(nint display, uint button, bool is_press, ulong delay);
    
    [DllImport(LibraryName)]
    public static extern void XTestFakeKeyEvent(nint display, uint keycode, bool is_press, ulong delay);
    
    [DllImport(LibraryName)]
    public static extern void XTestFakeMotionEvent(nint display, int screen_number, int x, int y, ulong delay);
    
    [DllImport(LibraryName)]
    public static extern void XTestFakeRelativeMotionEvent(nint display, int x, int y, ulong delay);

    [DllImport(LibraryName)]
    public static extern bool XTestQueryExtension(nint display, out int event_base, out int error_base, out int major_version, out int minor_version);
}
