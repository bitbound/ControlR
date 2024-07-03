using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Hubs;

public interface IViewerHub
{
    Task<bool> CheckIfServerAdministrator();

    Task<Result> ClearAlert(SignedPayloadDto signedDto);

    Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(string agentConnectionId, SignedPayloadDto requestDto);

    Task<Result<AgentAppSettings>> GetAgentAppSettings(string agentConnectionId, SignedPayloadDto signedDto);

    Task<Result<AlertBroadcastDto>> GetCurrentAlert();

    Task<Result<ServerStatsDto>> GetServerStats();

    Task<Uri?> GetWebSocketBridgeOrigin();
    Task<WindowsSession[]> GetWindowsSessions(string agentConnectionId, SignedPayloadDto signedDto);

    Task<Result> RequestStreamingSession(string agentConnectionId, SignedPayloadDto sessionRequestDto);
    Task<Result> SendAgentAppSettings(string agentConnectionId, SignedPayloadDto signedDto);

    Task<Result> SendAlertBroadcast(SignedPayloadDto signedDto);

    Task SendSignedDtoToAgent(string deviceId, SignedPayloadDto signedDto);

    Task SendSignedDtoToPublicKeyGroup(SignedPayloadDto signedDto);

    Task SendSignedDtoToStreamer(string streamerConnectionId, SignedPayloadDto signedDto);
    Task<Result> SendTerminalInput(string agentConnectionId, SignedPayloadDto dto);

}