using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.Linux;

public static unsafe class LibXfixes
{
  private const string LibXfixesSo = "libXfixes.so.3";
  
  [DllImport(LibXfixesSo)]
  public static extern IntPtr XFixesGetCursorImage(IntPtr display);

  [DllImport(LibXfixesSo)]
  public static extern int XFixesQueryVersion(IntPtr display, out int major_version, out int minor_version);


  [StructLayout(LayoutKind.Sequential)]
  public struct XFixesCursorImage
  {
    public short x;           // X position of the cursor on the screen
    public short y;           // Y position of the cursor on the screen
    public ushort width;      // Width of the cursor image
    public ushort height;     // Height of the cursor image
    public ushort xhot;       // X hotspot coordinate
    public ushort yhot;       // Y hotspot coordinate
    public ulong cursor_serial; // Serial number of the cursor
    public IntPtr pixels;     // Pointer to pixel data (unsigned long array)
    public ulong atom;        // Atom representing the cursor name (not IntPtr)
    public IntPtr name;       // Pointer to cursor name string
  }  
}