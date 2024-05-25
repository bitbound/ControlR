using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
public class IdentityDto
{
    [MsgPackKey]
    public required string Username { get; init; }
}
