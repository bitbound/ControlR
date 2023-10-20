using System.Runtime.InteropServices;

namespace ControlR.Viewer.Models;

[StructLayout(LayoutKind.Explicit)]
public struct DrawUnmarshalledInfo
{
    [FieldOffset(0)]
    public int Left;

    [FieldOffset(4)]
    public int Top;

    [FieldOffset(8)]
    public int Width;

    [FieldOffset(12)]
    public int Height;

    [FieldOffset(16)]
    public string CanvasId;
}
