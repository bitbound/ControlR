using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Libraries.Shared.Interfaces.HubClients;

public interface IAgentHubClient : IHubClient
{
    Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(SignedPayloadDto requestDto);

    Task<Result<AgentAppSettings>> GetAgentAppSettings(SignedPayloadDto signedDto);

    Task<bool> CreateStreamingSession(SignedPayloadDto sessionRequest);
    Task<WindowsSession[]> GetWindowsSessions(SignedPayloadDto signedDto);
    Task<Result> ReceiveAgentAppSettings(SignedPayloadDto signedDto);
    Task<Result> ReceiveTerminalInput(SignedPayloadDto dto);
}