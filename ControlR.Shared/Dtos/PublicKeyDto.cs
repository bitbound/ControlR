using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
public class PublicKeyDto
{
    [MsgPackKey]
    public required string Username { get; init; }

    [MsgPackKey]
    public required byte[] PublicKey { get; init; }
}
