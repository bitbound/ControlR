using System.Threading.Channels;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Hubs;

public interface IDeviceAccessHub : IBrowserHubBase
{
  Task<Result> CloseChatSession(Guid deviceId, Guid sessionId, int targetProcessId);
  Task CloseTerminalSession(Guid deviceId, Guid terminalSessionId);

  Task<Result> CreateTerminalSession(
    Guid deviceId,
    Guid terminalSessionId);

  Task<DesktopSession[]> GetActiveDesktopSessions(Guid deviceId);
  Task<Result<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto request);
  Task InvokeCtrlAltDel(Guid deviceId);

  Task<Result> RequestStreamingSession(Guid deviceId, RemoteControlSessionRequestDto sessionRequestDto);
  Task<Result> SendChatMessage(Guid deviceId, ChatMessageHubDto dto);
  Task<Result> SendTerminalInput(Guid deviceId, TerminalInputDto dto);
  Task<Result> UploadFile(FileUploadMetadata metadata, ChannelReader<byte[]> fileStream);
}