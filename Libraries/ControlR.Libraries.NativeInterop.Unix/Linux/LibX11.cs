/*

Copyright 1985, 1986, 1987, 1991, 1998  The Open Group

Permission to use, copy, modify, distribute, and sell this software and its
documentation for any purpose is hereby granted without fee, provided that
the above copyright notice appear in all copies and that both that
copyright notice and this permission notice appear in supporting
documentation.

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE
OPEN GROUP BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN
AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

Except as contained in this notice, the name of The Open Group shall not be
used in advertising or otherwise to promote the sale, use or other dealings
in this Software without prior written authorization from The Open Group.

*/

using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.Linux;

public static unsafe class LibX11
{
    public const int AnyPropertyType = 0;
    public const nint CurrentTime = 0;
    public const long NoEventMask = 0;
    public const int PropModeReplace = 0;
    public const long PropertyChangeMask = 1L << 22;
    public const int SelectionClear = 29;

    // X11 Constants
    public const int SelectionNotify = 31;
    public const int SelectionRequest = 30;
    private const string LibraryName = "libX11.so.6";

    public static string? GetStringFromAtom(nint display, nint atom)
    {
        var ptr = XGetAtomName(display, atom);
        if (ptr == nint.Zero)
            return null;

        try
        {
            return Marshal.PtrToStringUTF8(ptr);
        }
        finally
        {
            XFree(ptr);
        }
    }

    [DllImport(LibraryName)]
    public static extern void XChangeProperty(nint display, nint window, nint property, nint type, int format, int mode, nint data, int nelements);
    [DllImport(LibraryName)]
    public static extern void XCloseDisplay(nint display);

    [DllImport(LibraryName)]
    public static extern void XConvertSelection(nint display, nint selection, nint target, nint property, nint requestor, nint time);

    [DllImport(LibraryName)]
    public static extern nint XCreateBitmapFromData(nint display, nint drawable, nint data, uint width, uint height);

    [DllImport(LibraryName)]
    public static extern nint XCreateSimpleWindow(nint display, nint parent, int x, int y, uint width, uint height, uint border_width, ulong border, ulong background);
    [DllImport(LibraryName)]
    public static extern nint XDefaultGC(nint display, int screen_number);
    [DllImport(LibraryName)]
    public static extern nint XDefaultRootWindow(nint display);
    [DllImport(LibraryName)]
    public static extern int XDefaultScreen(nint display);

    [DllImport(LibraryName)]
    public static extern nint XDefaultVisual(nint display, int screen_number);
    [DllImport(LibraryName)]
    public static extern void XDestroyImage(nint ximage);

    [DllImport(LibraryName)]
    public static extern void XDestroyWindow(nint display, nint window);
    [DllImport(LibraryName)]
    public static extern int XDisplayHeight(nint display, int screen_number);
    [DllImport(LibraryName)]
    public static extern int XDisplayWidth(nint display, int screen_number);

    [DllImport(LibraryName)]
    public static extern void XFlush(nint display);
    [DllImport(LibraryName)]
    public static extern void XForceScreenSaver(nint display, int mode);

    [DllImport(LibraryName)]
    public static extern void XFree(nint data);

    [DllImport(LibraryName)]
    public static extern nint XGetAtomName(nint display, nint atom);

    [DllImport(LibraryName)]
    public static extern nint XGetImage(nint display, nint drawable, int x, int y, int width, int height, long plane_mask, int format);
    [DllImport(LibraryName)]
    public static extern void XGetInputFocus(nint display, out nint focus_return, out int revert_to_return);

    [DllImport(LibraryName)]
    public static extern nint XGetSelectionOwner(nint display, nint selection);

    [DllImport(LibraryName)]
    public static extern nint XGetSubImage(nint display, nint drawable, int x, int y, uint width, uint height, ulong plane_mask, int format, nint dest_image, int dest_x, int dest_y);

    [DllImport(LibraryName)]
    public static extern int XGetWindowAttributes(nint display, nint window, out XWindowAttributes windowAttributes);

    [DllImport(LibraryName)]
    public static extern int XGetWindowProperty(nint display, nint window, nint property, long long_offset, long long_length, bool delete, nint req_type, out nint actual_type_return, out int actual_format_return, out ulong nitems_return, out ulong bytes_after_return, out nint prop_return);
    [DllImport(LibraryName)]
    public static extern int XHeightOfScreen(nint screen);

    [DllImport(LibraryName)]
    public static extern nint XInternAtom(nint display, string atom_name, bool only_if_exists);
    [DllImport(LibraryName)]
    public static extern uint XKeysymToKeycode(nint display, nint keysym);

    [DllImport(LibraryName)]
    public static extern int XNextEvent(nint display, out XEvent event_return);
    [DllImport(LibraryName)]
    public static extern ulong XNextRequest(nint display);

    [DllImport(LibraryName)]
    public static extern void XNoOp(nint display);
    [DllImport(LibraryName)]
    public static extern nint XOpenDisplay(string? display_name);

    [DllImport(LibraryName)]
    public static extern int XPending(nint display);
    [DllImport(LibraryName)]
    public static extern nint XRootWindow(nint display, int screen_number);

    [DllImport(LibraryName)]
    public static extern nint XRootWindowOfScreen(nint screen);
    [DllImport(LibraryName)]
    public static extern int XScreenCount(nint display);
    [DllImport(LibraryName)]
    public static extern nint XScreenOfDisplay(nint display, int screen_number);

    [DllImport(LibraryName)]
    public static extern int XSelectInput(nint display, nint window, long event_mask);

    [DllImport(LibraryName)]
    public static extern void XSendEvent(nint display, nint window, bool propagate, long event_mask, ref XEvent event_send);

    [DllImport(LibraryName)]
    public static extern void XSetSelectionOwner(nint display, nint selection, nint owner, nint time);
    [DllImport(LibraryName)]
    public static extern nint XStringToKeysym(string key);
    [DllImport(LibraryName)]
    public static extern void XSync(nint display, bool discard);
    [DllImport(LibraryName)]
    public static extern int XWidthOfScreen(nint screen);

    // ===================================================================================================
    // CRITICAL WARNING: DO NOT REORDER STRUCT FIELDS!
    // 
    // These structs must match the exact binary layout expected by the X11 library.
    // Reordering fields will cause memory corruption, crashes, and undefined behavior.
    // Each struct is decorated with [StructLayout(LayoutKind.Sequential)] to enforce this.
    // 
    // If code cleanup tools attempt to sort these fields alphabetically, it WILL BREAK the ABI.
    // ===================================================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct XEvent
    {
        public int type;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        public long[] pad;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XImage
    {
        public int width;                 /* size of image */
        public int height;                /* size of image */
        public int xoffset;               /* number of pixels offset in X direction */
        public int format;                /* XYBitmap, XYPixmap, ZPixmap */
        public nint data;                /* pointer to image data */
        public int byte_order;            /* data byte order, LSBFirst, MSBFirst */
        public int bitmap_unit;           /* quant. of scanline 8, 16, 32 */
        public int bitmap_bit_order;      /* LSBFirst, MSBFirst */
        public int bitmap_pad;            /* 8, 16, 32 either XY or ZPixmap */
        public int depth;                 /* depth of image */
        public int bytes_per_line;        /* accelerator to next scanline */
        public int bits_per_pixel;        /* bits per pixel (ZPixmap) */
        public ulong red_mask;            /* bits in z arrangement */
        public ulong green_mask;
        public ulong blue_mask;
        public nint obdata;               /* hook for the object routines to hang on */
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XSelectionEvent
    {
        public int type;
        public ulong serial;
        public bool send_event;
        public nint display;
        public nint requestor;
        public nint selection;
        public nint target;
        public nint property;
        public nint time;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XSelectionRequestEvent
    {
        public int type;
        public ulong serial;
        public bool send_event;
        public nint display;
        public nint owner;
        public nint requestor;
        public nint selection;
        public nint target;
        public nint property;
        public nint time;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XWindowAttributes
    {
        public int x;
        public int y;
        public int width;
        public int height;
        public int border_width;
        public int depth;
        public nint visual;
        public nint root;
        public int @class;
        public int bit_gravity;
        public int win_gravity;
        public ulong backing_planes;
        public ulong backing_pixel;
        public bool save_under;
        public nint colormap;
        public bool map_installed;
        public int map_state;
        public long all_event_masks;
        public long do_not_propagate_mask;
        public long your_event_mask;
        public bool override_redirect;
        public nint screen;
        public int backing_store;
    }
}
