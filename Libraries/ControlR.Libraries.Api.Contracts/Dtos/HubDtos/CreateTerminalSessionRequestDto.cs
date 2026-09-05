namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record CreateTerminalSessionRequestDto(
  Guid DeviceId,
  Guid TerminalId);
