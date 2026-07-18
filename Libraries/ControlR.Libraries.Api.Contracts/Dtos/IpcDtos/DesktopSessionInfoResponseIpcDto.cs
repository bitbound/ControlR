using ControlR.Libraries.Api.Contracts.Dtos.Devices;

namespace ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record DesktopSessionInfoResponseIpcDto(
  bool AreRemoteControlPermissionsGranted,
  string DesktopName,
  string Name,
  int SystemSessionId,
  DesktopSessionType SessionType,
  string Username);
