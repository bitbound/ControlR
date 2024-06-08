using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Libraries.Shared.Hubs;

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
    Task<Result> RequestStreamingSession(string agentConnectionId, SignedPayloadDto sessionRequestDto);
    Task<WindowsSession[]> GetWindowsSessions(string agentConnectionId, SignedPayloadDto signedDto);

    Task<Result> SendAgentAppSettings(string agentConnectionId, SignedPayloadDto signedDto);

    Task<Result> SendAlertBroadcast(SignedPayloadDto signedDto);

    Task SendSignedDtoToAgent(string deviceId, SignedPayloadDto signedDto);

    Task SendSignedDtoToPublicKeyGroup(SignedPayloadDto signedDto);

    Task SendSignedDtoToStreamer(string streamerConnectionId, SignedPayloadDto signedDto);
    Task<Result> SendTerminalInput(string agentConnectionId, SignedPayloadDto dto);

}