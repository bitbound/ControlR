using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Shared.Models;

[MessagePackObject]
public class KeypairExport
{
    [MsgPackKey]
    public required byte[] EncryptedPrivateKey { get; init; }

    [MsgPackKey]
    public required byte[] PublicKey { get; init; }

    [MsgPackKey]
    public required string Username { get; init; }
}
