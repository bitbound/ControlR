namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record AssistConsentRequestedDto(
  Guid SessionId,
  string ViewerDisplayName,
  string? ViewerEmail);
