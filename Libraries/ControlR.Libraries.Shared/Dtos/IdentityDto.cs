using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public class IdentityDto
{
    [MsgPackKey]
    public required string Username { get; init; }
}
