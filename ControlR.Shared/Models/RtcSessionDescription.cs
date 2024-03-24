using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Shared.Models;

[MessagePackObject]
public class RtcSessionDescription
{
    [MsgPackKey]
    public string Sdp { get; init; } = string.Empty;

    [MsgPackKey]
    public string Type { get; init; } = string.Empty;
}
