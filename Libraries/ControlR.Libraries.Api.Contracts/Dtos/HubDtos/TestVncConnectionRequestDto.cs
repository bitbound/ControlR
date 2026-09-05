namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record TestVncConnectionRequestDto(
  Guid DeviceId,
  int Port);
