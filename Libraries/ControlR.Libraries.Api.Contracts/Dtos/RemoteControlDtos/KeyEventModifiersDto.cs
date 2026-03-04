using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record KeyEventModifiersDto(
  bool Control,
  bool Shift,
  bool Alt,
  bool Meta)
{
  public static readonly KeyEventModifiersDto None = new(false, false, false, false);

  [IgnoreMember]
  [JsonIgnore]
  public bool AreAnyPressed => Control || Shift || Alt || Meta;
}