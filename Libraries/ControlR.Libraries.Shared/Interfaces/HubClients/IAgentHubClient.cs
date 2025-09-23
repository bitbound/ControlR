using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Interfaces.HubClients;

public interface IAgentHubClient : IHubClient
{
  Task<Result> CreateRemoteControlSession(RemoteControlSessionRequestDto dto);
  Task<Result> CreateTerminalSession(TerminalSessionRequest requestDto);
  Task<Result> CreateVncSession(VncSessionRequestDto sessionRequestDto);
  Task<Result<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto request);
  Task<DeviceUiSession[]> GetActiveUiSessions();
  Task<Result> ReceiveTerminalInput(TerminalInputDto dto);
  Task<Result> RequestDesktopPreview(DesktopPreviewRequestDto dto);
  Task<Result> SendChatMessage(ChatMessageHubDto dto);
  Task<Result> CloseChatSession(Guid sessionId, int targetProcessId);
  Task UninstallAgent(string reason);
  Task<Result<GetRootDrivesResponseDto>> GetRootDrives(GetRootDrivesRequestDto requestDto);
  Task<Result> StreamDirectoryContents(DirectoryContentsStreamRequestHubDto dto);
  Task<Result> StreamSubdirectories(SubdirectoriesStreamRequestHubDto dto);
  Task<Result?> ReceiveFileUpload(FileUploadHubDto dto);
  Task<Result> SendFileDownload(FileDownloadHubDto dto);
  Task<Result> DeleteFile(FileDeleteHubDto dto);
  Task<Result> CreateDirectory(CreateDirectoryHubDto dto);
  Task<ValidateFilePathResponseDto> ValidateFilePath(ValidateFilePathHubDto dto);
  Task<PathSegmentsResponseDto> GetPathSegments(GetPathSegmentsHubDto dto);
}