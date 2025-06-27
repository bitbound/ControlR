using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Interfaces.HubClients;

public interface IAgentHubClient : IHubClient
{
  Task<bool> CreateStreamingSession(StreamerSessionRequestDto dto);
  Task<Result> CreateTerminalSession(TerminalSessionRequest requestDto);
  Task<Result> CreateVncSession(VncSessionRequestDto sessionRequestDto);
  Task<Result<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto request);
  Task<WindowsSession[]> GetWindowsSessions();
  Task<Result> ReceiveTerminalInput(TerminalInputDto dto);
  Task UninstallAgent(string reason);
}