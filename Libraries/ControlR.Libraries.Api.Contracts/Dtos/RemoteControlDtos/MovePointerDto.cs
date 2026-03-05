using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record MovePointerDto(
  double NormalizedX,
  double NormalizedY);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MovePointerType
{
  Absolute,
  Relative
}