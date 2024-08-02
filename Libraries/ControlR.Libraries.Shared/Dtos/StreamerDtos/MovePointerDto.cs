using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.SidecarDtos;

[MessagePackObject]
public record MovePointerDto(
    [property: MsgPackKey] double PercentX,
    [property: MsgPackKey] double PercentY) : DtoRecordBase;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MovePointerType
{
    Absolute,
    Relative
}