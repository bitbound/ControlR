using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.SidecarDtos;
public record MovePointerDto(double X, double Y, MovePointerType MoveType) : SidecarDtoBase(SidecarDtoType.MovePointer);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MovePointerType
{
    Absolute,
    Relative
}