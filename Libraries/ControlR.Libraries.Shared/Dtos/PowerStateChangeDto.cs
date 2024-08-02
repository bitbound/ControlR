using ControlR.Libraries.Shared.Enums;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
[method: SerializationConstructor]
[method: JsonConstructor]
public class PowerStateChangeDto(PowerStateChangeType type) : DtoBase
{
    [MsgPackKey]
    public PowerStateChangeType Type { get; init; } = type;
}
