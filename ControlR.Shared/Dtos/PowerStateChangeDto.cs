using ControlR.Shared.Enums;
using ControlR.Shared.Serialization;
using MessagePack;
using System.Text.Json.Serialization;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
[method: SerializationConstructor]
[method: JsonConstructor]
public class PowerStateChangeDto(PowerStateChangeType type)
{
    [MsgPackKey]
    public PowerStateChangeType Type { get; init; } = type;
}
