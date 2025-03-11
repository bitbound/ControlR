using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Interfaces.HubClients;

public interface IAgentHubClient : IHubClient
{
  Task<bool> CreateStreamingSession(StreamerSessionRequestDto dto);
  Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(TerminalSessionRequest requestDto);

  Task<WindowsSession[]> GetWindowsSessions();
  Task<Result> ReceiveTerminalInput(TerminalInputDto dto);
  Task UninstallAgent(string reason);
}