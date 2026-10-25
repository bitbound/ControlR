using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Ipc.Interfaces;

namespace ControlR.Agent.Common.Services;

public class AgentRpcService(IHubConnection<IAgentHub> hubConnection, ILogger<AgentRpcService> logger) : IAgentRpcService
{
    private readonly IHubConnection<IAgentHub> _hubConnection = hubConnection;
    private readonly ILogger<AgentRpcService> _logger = logger;

    public async Task<bool> SendChatResponse(ChatResponseIpcDto dto)
    {
        try
        {
            var responseDto = new ChatResponseHubDto(
                dto.SessionId,
                dto.DesktopUiProcessId,
                dto.Message,
                dto.SenderUsername,
                dto.ViewerConnectionId,
                dto.Timestamp,
                dto.Kind);

            _logger.LogInformation(
                "Sending chat response for session {SessionId} from {Username}",
                responseDto.SessionId,
                responseDto.SenderUsername);

            return await _hubConnection.Server.SendChatResponse(responseDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while handling chat response for session {SessionId}.", dto.SessionId);
            return false;
        }
    }

    public async Task SendRemoteControlDto(string viewerConnectionId, RemoteControlDesktopWrapper dto)
    {
        try
        {
            await _hubConnection.Server.SendRemoteControlDtoToViewer(viewerConnectionId, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while forwarding remote control DTO to the viewer.");
        }
    }
}
