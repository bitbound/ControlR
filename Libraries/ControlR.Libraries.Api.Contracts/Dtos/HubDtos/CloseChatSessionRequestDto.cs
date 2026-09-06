namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record CloseChatSessionRequestDto(
  Guid DeviceId,
  Guid SessionId,
  int TargetProcessId);
