using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Serialization;
using MessagePack;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
[method: SerializationConstructor]
[method: JsonConstructor]
public class PowerStateChangeDto(PowerStateChangeType type)
{
    [MsgPackKey]
    public PowerStateChangeType Type { get; init; } = type;
}
