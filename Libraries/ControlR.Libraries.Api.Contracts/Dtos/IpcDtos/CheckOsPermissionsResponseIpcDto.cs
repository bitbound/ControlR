namespace ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;

/// <summary>
/// IPC response containing the status of OS-level desktop client permissions.
/// Returns whether the requested desktop client operation is allowed and an optional reason.
/// </summary>
[MessagePackObject(keyAsPropertyName: true)]
public record CheckOsPermissionsResponseIpcDto(
  bool ArePermissionsGranted,
  string? Reason = null);
