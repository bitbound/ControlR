using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
public record UserPreferenceResponseDto(Guid? Id, string Name, string? Value)
{
  [JsonIgnore]
  [IgnoreMember]
  public bool HasValueSet => !string.IsNullOrWhiteSpace(Value);
};