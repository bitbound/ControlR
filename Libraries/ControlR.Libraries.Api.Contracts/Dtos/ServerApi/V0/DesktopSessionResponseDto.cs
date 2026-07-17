using ControlR.Libraries.Api.Contracts.Dtos.Devices;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record DesktopSessionResponseDto(
  bool AreRemoteControlPermissionsGranted,
  string Name,
  int ProcessId,
  int SystemSessionId,
  DesktopSessionType Type,
  string Username)
{
  public static DesktopSessionResponseDto From(DesktopSession session)
  {
    return new(
      session.AreRemoteControlPermissionsGranted,
      session.Name,
      session.ProcessId,
      session.SystemSessionId,
      session.Type,
      session.Username);
  }
}
