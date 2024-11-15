using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Hubs;

public interface IViewerHub
{
  Task<bool> CheckIfServerAdministrator();

  Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(
    Guid deviceId,
    TerminalSessionRequest requestDto);

  Task<Result<AgentAppSettings>> GetAgentAppSettings(Guid deviceId);

  Task<Result<ServerStatsDto>> GetServerStats();

  Task<Uri?> GetWebSocketBridgeOrigin();
  Task<WindowsSession[]> GetWindowsSessions(Guid deviceId);

  Task<Result> RequestStreamingSession(Guid deviceId, StreamerSessionRequestDto sessionRequestDto);
  Task<Result> SendAgentAppSettings(Guid deviceId, AgentAppSettings signedDto);
  Task SendDtoToAgent(Guid deviceId, DtoWrapper wrapper);
  Task SendDtoToUserGroups(DtoWrapper wrapper);
  Task<Result> SendTerminalInput(Guid deviceId, TerminalInputDto dto);
  Task UninstallAgent(Guid deviceId, string reason);
}