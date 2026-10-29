namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record RespondToAssistConsentRequestDto(
  Guid SessionId,
  bool Approved,
  Guid ConsentNonce = default);
