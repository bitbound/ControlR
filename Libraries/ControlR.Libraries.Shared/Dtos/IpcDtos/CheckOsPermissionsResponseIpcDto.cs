namespace ControlR.Libraries.Shared.Dtos.IpcDtos;

/// <summary>
/// IPC response containing the status of OS-level remote control permissions.
/// Returns whether the necessary permissions for remote control are granted
/// (e.g., macOS Accessibility/Screen Recording, Linux Wayland permissions).
/// </summary>
[MessagePackObject(keyAsPropertyName: true)]
public record CheckOsPermissionsResponseIpcDto(
  bool ArePermissionsGranted);
