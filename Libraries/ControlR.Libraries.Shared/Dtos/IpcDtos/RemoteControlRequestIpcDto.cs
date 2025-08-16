namespace ControlR.Libraries.Shared.Dtos.IpcDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record RemoteControlRequestIpcDto(
  Guid SessionId,
  Uri WebsocketUri,
  int TargetSystemSession,
  int TargetProcessId,
  string ViewerConnectionId,
  Guid DeviceId,
  bool NotifyUserOnSessionStart,
  string DataFolder,
  string ViewerName = "");