using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Web.Server.Models;

[MessagePackObject]
public class CounterToken
{
    [MsgPackKey]
    public int Count { get; set; }

    [MsgPackKey]
    public DateTimeOffset LastUpdated { get; set; }

    [MsgPackKey]
    public required Guid NodeId { get; init; }
}
