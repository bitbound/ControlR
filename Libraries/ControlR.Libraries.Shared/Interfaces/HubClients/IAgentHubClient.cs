using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Interfaces.HubClients;

public interface IAgentHubClient : IHubClient
{
    Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(TerminalSessionRequest requestDto);

    Task<Result<AgentAppSettings>> GetAgentAppSettings();

    Task<bool> CreateStreamingSession(StreamerSessionRequestDto dto);
    Task<WindowsSession[]> GetWindowsSessions();
    Task<Result> ReceiveAgentAppSettings(AgentAppSettings appSettings);
    Task<Result> ReceiveTerminalInput(TerminalInputDto dto);
}