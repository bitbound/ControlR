using ControlR.Shared.Dtos;
using ControlR.Shared.Models;

namespace ControlR.Shared.Hubs;

public interface IViewerHub
{
    Task<bool> CheckIfServerAdministrator();
    Task<bool> CheckIfStoreIntegrationEnabled();
    Task<Result> ClearAlert(SignedPayloadDto signedDto);

    Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(string agentConnectionId, SignedPayloadDto requestDto);

    Task<Result<AgentAppSettings>> GetAgentAppSettings(string agentConnectionId, SignedPayloadDto signedDto);

    Task<Result<AlertBroadcastDto>> GetCurrentAlert();

    Task<IceServer[]> GetIceServers();

    Task<Result<ServerStatsDto>> GetServerStats();
    Task<Result<StreamerHubSession>> GetStreamingSession(string agentConnectionId, Guid streamingSessionId, SignedPayloadDto sessionRequestDto);
    Task<WindowsSession[]> GetWindowsSessions(string agentConnectionId, SignedPayloadDto signedDto);

    Task<Result> SendAgentAppSettings(string agentConnectionId, SignedPayloadDto signedDto);

    Task<Result> SendAlertBroadcast(SignedPayloadDto signedDto);

    Task SendSignedDtoToAgent(string deviceId, SignedPayloadDto signedDto);

    Task SendSignedDtoToPublicKeyGroup(SignedPayloadDto signedDto);

    Task SendSignedDtoToStreamer(Guid streamingSessionId, SignedPayloadDto signedDto);
    Task<Result> SendTerminalInput(string agentConnectionId, SignedPayloadDto dto);

}