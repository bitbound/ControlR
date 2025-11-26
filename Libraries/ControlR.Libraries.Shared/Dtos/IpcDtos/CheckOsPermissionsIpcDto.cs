namespace ControlR.Libraries.Shared.Dtos.IpcDtos;

/// <summary>
/// IPC request to check OS-level remote control permissions status on the desktop client.
/// </summary>
[MessagePackObject(keyAsPropertyName: true)]
public record CheckOsPermissionsIpcDto(
  int TargetProcessId);
