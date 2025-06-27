using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Hubs;

public interface IViewerHub
{
  Task<bool> CheckIfServerAdministrator();

  Task<Result> CreateTerminalSession(
    Guid deviceId,
    TerminalSessionRequest requestDto);
  Task<Result<PwshCompletionsResponseDto>> GetPwshCompletions(Guid deviceId, PwshCompletionsRequestDto request);
  Task<Result<ServerStatsDto>> GetServerStats();

  Task<Uri?> GetWebSocketRelayOrigin();
  Task<WindowsSession[]> GetWindowsSessions(Guid deviceId);

  Task<Result> RequestStreamingSession(Guid deviceId, StreamerSessionRequestDto sessionRequestDto);
  Task<Result> RequestVncSession(Guid deviceId, VncSessionRequestDto sessionRequestDto);
  Task SendDtoToAgent(Guid deviceId, DtoWrapper wrapper);
  Task SendDtoToUserGroups(DtoWrapper wrapper);
  Task<Result> SendTerminalInput(Guid deviceId, TerminalInputDto dto);
  Task UninstallAgent(Guid deviceId, string reason);
}