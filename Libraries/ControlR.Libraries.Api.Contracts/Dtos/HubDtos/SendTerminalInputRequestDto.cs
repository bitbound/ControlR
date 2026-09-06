namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record SendTerminalInputRequestDto(
  Guid DeviceId,
  TerminalInputDto Input);
