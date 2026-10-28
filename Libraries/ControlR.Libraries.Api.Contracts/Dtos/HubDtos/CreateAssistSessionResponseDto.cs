namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record CreateAssistSessionResponseDto(
  string Code,
  Guid SessionId);
