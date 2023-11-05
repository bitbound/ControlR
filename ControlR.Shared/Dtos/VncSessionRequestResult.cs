using ControlR.Shared.Serialization;
using MessagePack;
using System.Text.Json.Serialization;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
[method: JsonConstructor]
[method: SerializationConstructor]
public class VncSessionRequestResult(bool sessionCreated, bool autoRunUsed = false)
{
    [MsgPackKey]
    public bool AutoRunUsed { get; init; } = autoRunUsed;

    [MsgPackKey]
    public bool SessionCreated { get; init; } = sessionCreated;
}