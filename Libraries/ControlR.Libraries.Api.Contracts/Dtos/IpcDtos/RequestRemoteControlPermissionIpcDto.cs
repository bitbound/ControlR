namespace ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

/// <summary>
/// IPC request to actively request (not just check) OS-level remote control permissions
/// on the desktop client. On Linux/Wayland, this triggers the XDG portal permission dialog.
/// </summary>
[MessagePackObject(keyAsPropertyName: true)]
public record RequestRemoteControlPermissionIpcDto(
  int TargetProcessId,
  DesktopClientPermissionScope Scope = DesktopClientPermissionScope.RemoteControl);
