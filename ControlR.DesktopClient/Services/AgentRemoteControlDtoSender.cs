using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.DesktopClient.Services;

/// <summary>
/// Hands a desktop-issued DTO to the agent that owns the session, which is how the desktop client
/// that runs under an agent reaches its viewer.
/// </summary>
public class AgentRemoteControlDtoSender(IIpcClientAccessor ipcClientAccessor) : IRemoteControlDtoSender
{
  private readonly IIpcClientAccessor _ipcClientAccessor = ipcClientAccessor;

  public async Task<bool> TrySendRemoteControlDto(
    string viewerConnectionId,
    RemoteControlDesktopWrapper wrapper)
  {
    if (!_ipcClientAccessor.TryGetClient(out var client))
    {
      return false;
    }

    await client.Server.SendRemoteControlDto(viewerConnectionId, wrapper);
    return true;
  }
}
