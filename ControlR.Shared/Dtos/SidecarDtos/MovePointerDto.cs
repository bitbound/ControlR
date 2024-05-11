using System.Text.Json.Serialization;

namespace ControlR.Shared.Dtos.SidecarDtos;
public record MovePointerDto(int X, int Y, MovePointerType MoveType) : SidecarDtoBase(SidecarDtoType.MovePointer);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MovePointerType
{
    Absolute,
    Relative
}