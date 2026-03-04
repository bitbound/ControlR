using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record MovePointerDto(
    double PercentX,
    double PercentY);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MovePointerType
{
  Absolute,
  Relative
}