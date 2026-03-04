using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Hubs.Clients;

public interface IAgentHubClient : IHubClient
{
  Task<HubResult> CloseChatSession(Guid sessionId, int targetProcessId);
  Task CloseTerminalSession(Guid terminalSessionId);
  Task<HubResult> CreateDirectory(CreateDirectoryHubDto dto);
  Task<HubResult> CreateRemoteControlSession(RemoteControlSessionRequestDto dto);
  Task<HubResult> CreateTerminalSession(Guid terminalSessionId, string viewerConnectionId);
  Task<HubResult> CreateVncSession(VncSessionRequestDto sessionRequestDto);
  Task<HubResult> DeleteFile(FileDeleteHubDto dto);
  Task<HubResult> DownloadFileFromViewer(FileUploadHubDto dto);
  Task<DesktopSession[]> GetActiveDesktopSessions();
  Task<HubResult<GetLogFilesResponseDto>> GetLogFiles();
  Task<PathSegmentsResponseDto> GetPathSegments(GetPathSegmentsHubDto dto);
  Task<HubResult<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto request);
  Task<HubResult<GetRootDrivesResponseDto>> GetRootDrives(GetRootDrivesRequestDto requestDto);
  Task<HubResult> InvokeCtrlAltDel(InvokeCtrlAltDelRequestDto requestDto);
  Task InvokeWakeDevice(WakeDeviceDto dto);
  Task ReceiveAgentUpdateTrigger();
  Task ReceivePowerStateChange(PowerStateChangeType changeType);
  Task<HubResult> ReceiveTerminalInput(TerminalInputDto dto);
  Task RefreshDeviceInfo();
  Task<HubResult> RequestDesktopPreview(DesktopPreviewRequestDto dto);
  Task<HubResult> SendChatMessage(ChatMessageHubDto dto);
  Task<HubResult> StreamDirectoryContents(DirectoryContentsStreamRequestHubDto dto);
  Task<HubResult> StreamFileContents(StreamFileContentsRequestHubDto dto);
  Task<HubResult> StreamSubdirectories(SubdirectoriesStreamRequestHubDto dto);
  Task<HubResult> TestVncConnection(int port);
  Task UninstallAgent(string reason);
  Task<HubResult<FileDownloadResponseHubDto>> UploadFileToViewer(FileDownloadHubDto dto);
  Task<ValidateFilePathResponseDto> ValidateFilePath(ValidateFilePathHubDto dto);
}