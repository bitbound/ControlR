namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record AssistSessionEndedDto(
  Guid SessionId,
  string Reason);
