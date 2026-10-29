namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record CancelAssistConsentRequestDto(Guid SessionId);
