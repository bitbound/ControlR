using ControlR.Shared.Dtos;
using ControlR.Shared.Models;
using ControlR.Shared.Primitives;

namespace ControlR.Shared.Interfaces.HubClients;

public interface IAgentHubClient : IHubClient
{
    Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(SignedPayloadDto requestDto);

    Task<Result<AgentAppSettings>> GetAgentAppSettings(SignedPayloadDto signedDto);

    Task<bool> CreateStreamingSession(SignedPayloadDto sessionRequest);
    Task<WindowsSession[]> GetWindowsSessions(SignedPayloadDto signedDto);
    Task<Result> ReceiveAgentAppSettings(SignedPayloadDto signedDto);
    Task<Result> ReceiveTerminalInput(SignedPayloadDto dto);
}