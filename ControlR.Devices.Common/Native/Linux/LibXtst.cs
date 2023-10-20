using System.Runtime.InteropServices;

namespace ControlR.Devices.Common.Native.Linux;

public partial class LibXtst
{
    [LibraryImport("libXtst")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool XTestQueryExtension(IntPtr display, out int event_base, out int error_base, out int major_version, out int minor_version);

    [LibraryImport("libXtst")]
    internal static partial void XTestFakeKeyEvent(IntPtr display, uint keycode, [MarshalAs(UnmanagedType.Bool)] bool is_press, ulong delay);

    [LibraryImport("libXtst")]
    internal static partial void XTestFakeButtonEvent(IntPtr display, uint button, [MarshalAs(UnmanagedType.Bool)] bool is_press, ulong delay);

    [LibraryImport("libXtst")]
    internal static partial void XTestFakeMotionEvent(IntPtr display, int screen_number, int x, int y, ulong delay);
}
