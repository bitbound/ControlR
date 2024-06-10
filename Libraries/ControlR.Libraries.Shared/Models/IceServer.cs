using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Libraries.Shared.Models;

[MessagePackObject]
public class IceServer
{
    [MsgPackKey]
    public string Credential { get; init; } = string.Empty;

    [MsgPackKey]
    public string CredentialType { get; init; } = string.Empty;

    [MsgPackKey]
    public string Urls { get; init; } = string.Empty;

    [MsgPackKey]
    public string Username { get; init; } = string.Empty;
}