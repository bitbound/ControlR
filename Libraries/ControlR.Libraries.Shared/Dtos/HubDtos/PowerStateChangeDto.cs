using ControlR.Libraries.Shared.Enums;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
[method: SerializationConstructor]
[method: JsonConstructor]
public class PowerStateChangeDto(PowerStateChangeType type)
{
    [MsgPackKey]
    public PowerStateChangeType Type { get; init; } = type;
}
