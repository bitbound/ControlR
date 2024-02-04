using ControlR.Shared.Dtos;
using ControlR.Shared.Models;
using ControlR.Shared.Primitives;

namespace ControlR.Shared.Hubs;
public interface IViewerHub
{
    Task<Result<bool>> CheckIfServerAdministrator(SignedPayloadDto signedDto);
    Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(string agentConnectionId, SignedPayloadDto requestDto);
    Task<Result<AgentAppSettings>> GetAgentAppSettings(string agentConnectionId, SignedPayloadDto signedDto);
    Task<Result<int>> GetAgentCount(SignedPayloadDto signedDto);
    Task<VncSessionRequestResult> GetVncSession(string agentConnectionId, Guid sessionId, SignedPayloadDto sessionRequestDto);
    Task<WindowsSession[]> GetWindowsSessions(string agentConnectionId, SignedPayloadDto signedDto);
    Task<Result> SendAgentAppSettings(string agentConnectionId, SignedPayloadDto signedDto);
    Task SendSignedDtoToAgent(string deviceId, SignedPayloadDto signedDto);
    Task SendSignedDtoToPublicKeyGroup(SignedPayloadDto signedDto);
    Task<Result> SendTerminalInput(string agentConnectionId, SignedPayloadDto dto);
    Task<Result> StartRdpProxy(string agentConnectionId, Guid sessionId, SignedPayloadDto requestDto);
}