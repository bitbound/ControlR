using ControlR.Shared.Serialization;
using MessagePack;
using System.Text.Json.Serialization;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
[method: JsonConstructor]
[method: SerializationConstructor]
public class VncSessionRequestResult(bool sessionCreated, bool? autoInstallUsed = null)
{
    [MsgPackKey]
    public bool? AutoInstallUsed { get; init; } = autoInstallUsed;

    [MsgPackKey]
    public bool SessionCreated { get; init; } = sessionCreated;
}