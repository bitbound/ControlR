using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Shared.Models;

[MessagePackObject]
public class KeypairExport
{
    [MsgPackKey]
    public required string EncryptedPrivateKey { get; init; }

    [MsgPackKey]
    public required string PublicKey { get; init; }

    [MsgPackKey]
    public required string Username { get; init; }
}