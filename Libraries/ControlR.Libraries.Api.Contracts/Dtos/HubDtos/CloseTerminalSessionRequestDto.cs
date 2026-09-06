namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record CloseTerminalSessionRequestDto(
  Guid DeviceId,
  Guid TerminalId);
