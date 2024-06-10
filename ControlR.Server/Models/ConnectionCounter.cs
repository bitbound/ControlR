using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Server.Models;

[MessagePackObject]
public class ConnectionCounter
{
    [MsgPackKey]
    public int Count { get; set; }

    [MsgPackKey]
    public DateTimeOffset LastUpdated { get; set; }

    [MsgPackKey]
    public required Guid NodeId { get; init; }
}
