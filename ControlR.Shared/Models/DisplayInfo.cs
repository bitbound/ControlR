using ControlR.Shared.Serialization;
using MessagePack;
using System.Drawing;
using System.Numerics;

namespace ControlR.Shared.Models;

[MessagePackObject]
public class DisplayInfo
{
    [MsgPackKey]
    public bool IsPrimary { get; set; }
    [MsgPackKey]
    public Vector2 ScreenSize { get; set; }
    [MsgPackKey]
    public Rectangle MonitorArea { get; set; }
    [MsgPackKey]
    public Rectangle WorkArea { get; set; }
    [MsgPackKey]
    public string DeviceName { get; set; } = string.Empty;
    [MsgPackKey]
    public IntPtr Hmon { get; set; }
}
