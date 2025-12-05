using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Hubs.Clients;

public interface IAgentHubClient : IHubClient
{
  Task<Result> CloseChatSession(Guid sessionId, int targetProcessId);
  Task CloseTerminalSession(Guid terminalSessionId);
  Task<Result> CreateDirectory(CreateDirectoryHubDto dto);
  Task<Result> CreateRemoteControlSession(RemoteControlSessionRequestDto dto);
  Task<Result> CreateTerminalSession(Guid terminalSessionId, string viewerConnectionId);
  Task<Result> CreateVncSession(VncSessionRequestDto sessionRequestDto);
  Task<Result> DeleteFile(FileDeleteHubDto dto);
  Task<Result?> DownloadFileFromViewer(FileUploadHubDto dto);
  Task<DesktopSession[]> GetActiveDesktopSessions();
  Task<PathSegmentsResponseDto> GetPathSegments(GetPathSegmentsHubDto dto);
  Task<Result<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto request);
  Task<Result<GetRootDrivesResponseDto>> GetRootDrives(GetRootDrivesRequestDto requestDto);
  Task<Result> InvokeCtrlAltDel(InvokeCtrlAltDelRequestDto requestDto);
  Task InvokeWakeDevice(WakeDeviceDto dto);
  Task ReceiveAgentUpdateTrigger();
  Task ReceivePowerStateChange(PowerStateChangeType changeType);
  Task<Result> ReceiveTerminalInput(TerminalInputDto dto);
  Task RefreshDeviceInfo();
  Task<Result> RequestDesktopPreview(DesktopPreviewRequestDto dto);
  Task<Result> SendChatMessage(ChatMessageHubDto dto);
  Task<Result> StreamDirectoryContents(DirectoryContentsStreamRequestHubDto dto);
  Task<Result> StreamSubdirectories(SubdirectoriesStreamRequestHubDto dto);
  Task UninstallAgent(string reason);
  Task<Result<FileDownloadResponseHubDto>> UploadFileToViewer(FileDownloadHubDto dto);
  Task<ValidateFilePathResponseDto> ValidateFilePath(ValidateFilePathHubDto dto);
}