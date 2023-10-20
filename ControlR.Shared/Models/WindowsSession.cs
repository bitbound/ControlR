using ControlR.Shared.Serialization;
using MessagePack;
namespace ControlR.Shared.Models;


public enum SessionType
{
    Console = 0,
    RDP = 1
}

[MessagePackObject]
public class WindowsSession
{
    [MsgPackKey]
    public int Id { get; set; }

    [MsgPackKey]
    public string Name { get; set; } = string.Empty;

    [MsgPackKey]
    public SessionType Type { get; set; }

    [MsgPackKey]
    public string Username { get; set; } = string.Empty;
}
