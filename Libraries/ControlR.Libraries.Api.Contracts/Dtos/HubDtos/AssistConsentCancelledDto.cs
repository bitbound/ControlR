namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record AssistConsentCancelledDto(Guid SessionId);
