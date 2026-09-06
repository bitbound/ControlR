namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record UninstallAgentRequestDto(
  Guid DeviceId,
  string Reason);
