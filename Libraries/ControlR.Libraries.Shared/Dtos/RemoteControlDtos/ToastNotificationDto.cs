using ControlR.Libraries.Shared.Enums;
using MessagePack;

namespace ControlR.Libraries.Shared.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ToastNotificationDto(
  string Message,
  MessageSeverity Severity);
