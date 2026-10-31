using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

/// <summary>
/// Delivers a desktop-issued remote control DTO to the viewer. The desktop client that runs under
/// an agent hands the DTO to that agent, while a stand-alone client delivers it some other way and
/// reports failure when it has no way to.
/// </summary>
public interface IRemoteControlDtoSender
{
  /// <summary>
  /// Returns false when nothing is connected to carry the DTO.
  /// </summary>
  Task<bool> TrySendRemoteControlDto(
    string viewerConnectionId,
    RemoteControlDesktopWrapper wrapper);
}
