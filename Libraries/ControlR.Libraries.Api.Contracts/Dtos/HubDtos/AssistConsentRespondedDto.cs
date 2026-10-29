namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record AssistConsentRespondedDto(
  Guid SessionId,
  bool Approved,
  string? AccessToken,
  AssistConsentRespondedReason? Reason = null);
