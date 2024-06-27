using System.Runtime.InteropServices;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[StructLayout(LayoutKind.Explicit)]
public readonly struct StreamingFrameHeader
{
    public StreamingFrameHeader(
        int x,
        int y,
        int width,
        int height,
        int imageSize)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        ImageSize = imageSize;
    }

    public StreamingFrameHeader(byte[] buffer)
    {
        X = BitConverter.ToInt32(buffer, 0);
        Y = BitConverter.ToInt32(buffer, 4);
        Width = BitConverter.ToInt32(buffer, 8);
        Height = BitConverter.ToInt32(buffer, 12);
        ImageSize = BitConverter.ToInt32(buffer, 16);
    }

    [FieldOffset(0)]
    public readonly int X;

    [FieldOffset(4)]
    public readonly int Y;

    [FieldOffset(8)]
    public readonly int Width;

    [FieldOffset(12)]
    public readonly int Height;

    [FieldOffset(16)]
    public readonly int ImageSize;

    public static int Size => Marshal.SizeOf<StreamingFrameHeader>();
}
