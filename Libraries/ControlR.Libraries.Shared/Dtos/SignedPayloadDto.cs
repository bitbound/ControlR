using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public class SignedPayloadDto
{
    [MsgPackKey]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required DtoType DtoType { get; init; }

    [MsgPackKey]
    public required byte[] Payload { get; init; }

    [MsgPackKey]
    public required byte[] PublicKey { get; init; }

    [IgnoreDataMember]
    [IgnoreMember]
    public string PublicKeyBase64 => Convert.ToBase64String(PublicKey ?? []);

    [MsgPackKey]
    public required string PublicKeyPem { get; init; }

    [MsgPackKey]
    public required byte[] Signature { get; init; }

    [MsgPackKey]
    public required byte[] Timestamp { get; init; }

    [MsgPackKey]
    public required byte[] TimestampSignature { get; init; }
}