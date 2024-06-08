using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Libraries.Shared.Models;

public enum WindowsSessionType
{
    Console = 0,
    RDP = 1
}

[MessagePackObject]
public class WindowsSession
{
    [MsgPackKey]
    public uint Id { get; set; }

    [MsgPackKey]
    public string Name { get; set; } = string.Empty;

    [MsgPackKey]
    public WindowsSessionType Type { get; set; }

    [MsgPackKey]
    public string Username { get; set; } = string.Empty;
}