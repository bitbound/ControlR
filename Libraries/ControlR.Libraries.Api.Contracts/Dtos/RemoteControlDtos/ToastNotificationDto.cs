namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ToastNotificationDto(
  string Message,
  MessageSeverity Severity);
