using System.Threading.Channels;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Hubs;

public interface IViewerHub
{
  Task<Result> CloseChatSession(Guid deviceId, Guid sessionId, int targetProcessId);
  Task CloseTerminalSession(Guid deviceId, Guid terminalSessionId);

  Task<Result> CreateTerminalSession(
    Guid deviceId,
    Guid terminalSessionId);

  Task<DesktopSession[]> GetActiveDesktopSessions(Guid deviceId);
  Task<Result<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto request);
  Task InvokeCtrlAltDel(Guid deviceId);
  Task RefreshDeviceInfo(Guid deviceId);

  Task<Result> RequestStreamingSession(Guid deviceId, RemoteControlSessionRequestDto sessionRequestDto);
  Task<Result> RequestVncSession(Guid deviceId, VncSessionRequestDto sessionRequestDto);
  Task SendAgentUpdateTrigger(Guid deviceId);
  Task<Result> SendChatMessage(Guid deviceId, ChatMessageHubDto dto);
  Task SendDtoToAgent(Guid deviceId, DtoWrapper wrapper);
  Task SendDtoToUserGroups(DtoWrapper wrapper);
  Task SendPowerStateChange(Guid deviceId, PowerStateChangeType changeType);
  Task<Result> SendTerminalInput(Guid deviceId, TerminalInputDto dto);
  Task SendWakeDevice(Guid deviceId, string[] macAddresses);
  Task UninstallAgent(Guid deviceId, string reason);
  Task<Result> UploadFile(FileUploadMetadata metadata, ChannelReader<byte[]> fileStream);
}