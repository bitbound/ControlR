using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Agent.LoadTester;

public class TestAgentHubClient : IAgentHubClient
{
  public Task<bool> CreateStreamingSession(RemoteControlSessionRequestDto dto)
  {
    Console.WriteLine($"Creating streaming session with ID: {dto.SessionId}, Viewer: {dto.ViewerName}");
    return Task.FromResult(true);
  }

  public Task<Result> CreateTerminalSession(TerminalSessionRequest requestDto)
  {
    Console.WriteLine("Received terminal session request.");
    return Result.Ok().AsTaskResult();
  }

  public Task<Result> CreateVncSession(VncSessionRequestDto sessionRequestDto)
  {
    return Result.Ok().AsTaskResult();
  }

  public Task<Result<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto request)
  {
    return Task.FromResult(Result.Ok(new PwshCompletionsResponseDto(
      ReplacementIndex: 0,
      ReplacementLength: 0,
      CompletionMatches: [],
      HasMorePages: false,
      TotalCount: 0,
      CurrentPage: 0
    )));
  }

  public Task<DeviceUiSession[]> GetActiveUiSessions()
  {
    var session = new DeviceUiSession
    {
      SystemSessionId = 1,
      Name = "Console",
      Type = UiSessionType.Console,
      Username = "TestUser"
    };
    return Task.FromResult(new[] { session });
  }

  public Task ReceiveDto(DtoWrapper dtoWrapper)
  {
    Console.WriteLine($"Received DTO of type: {dtoWrapper.DtoType}");
    return Task.CompletedTask;
  }

  public Task<Result> ReceiveTerminalInput(TerminalInputDto dto)
  {
    Console.WriteLine($"Received terminal input: {dto.Input}");
    return Task.FromResult(Result.Ok());
  }

  public Task<Result> RequestDesktopPreview(DesktopPreviewRequestDto dto)
  {
    Console.WriteLine($"Desktop preview requested. Requester: {dto.RequesterId}, Stream: {dto.StreamId}, Process: {dto.TargetProcessId}");
    return Task.FromResult(Result.Ok());
  }

  public Task<Result> SendChatMessage(ChatMessageHubDto dto)
  {
    Console.WriteLine($"Sending chat message from {dto.SenderName} ({dto.SenderEmail}): {dto.Message}");
    return Task.FromResult(Result.Ok());
  }

  public Task<Result> CloseChatSession(Guid sessionId, int targetProcessId)
  {
    Console.WriteLine($"Closing chat session {sessionId} for process {targetProcessId}");
    return Task.FromResult(Result.Ok());
  }

  public Task UninstallAgent(string reason)
  {
    Console.WriteLine($"Uninstalling agent for reason: {reason}");
    return Task.CompletedTask;
  }

  public Task<Result<GetRootDrivesResponseDto>> GetRootDrives(GetRootDrivesRequestDto requestDto)
  {
    Console.WriteLine($"Getting root drives for device {requestDto.DeviceId}");
    var drives = new FileSystemEntryDto[]
    {
      new("C:", "C:\\", true, 0, DateTimeOffset.Now, false, true, true, true)
    };
    return Task.FromResult(Result.Ok(new GetRootDrivesResponseDto(drives)));
  }

  // Removed legacy GetSubdirectories/GetDirectoryContents methods (now always streamed).

  public Task<Result> StreamDirectoryContents(DirectoryContentsStreamRequestHubDto dto)
  {
    Console.WriteLine($"Streaming directory contents for {dto.DirectoryPath} (stream {dto.StreamId})");
    return Result.Ok().AsTaskResult();
  }

  public Task<Result> StreamSubdirectories(SubdirectoriesStreamRequestHubDto dto)
  {
    Console.WriteLine($"Streaming subdirectories for {dto.DirectoryPath} (stream {dto.StreamId})");
    return Result.Ok().AsTaskResult();
  }

  public Task<Result?> ReceiveFileUpload(FileUploadHubDto dto)
  {
    Console.WriteLine($"Received file upload request for {dto.FileName} to {dto.TargetDirectoryPath}");
    return Result.Ok().AsTaskResult<Result?>();
  }

  public Task<Result> SendFileDownload(FileDownloadHubDto dto)
  {
    Console.WriteLine($"Received file download request for {dto.FilePath}");
    return Result.Ok().AsTaskResult();
  }

  public Task<Result> DeleteFile(FileDeleteHubDto dto)
  {
    Console.WriteLine($"Received file delete request for {dto.TargetPath}");
    return Result.Ok().AsTaskResult();
  }

  public Task<Result> CreateDirectory(CreateDirectoryHubDto dto)
  {
    Console.WriteLine($"Received create directory request for {dto.DirectoryName} in {dto.ParentPath}");
    return Result.Ok().AsTaskResult();
  }

  public Task<ValidateFilePathResponseDto> ValidateFilePath(ValidateFilePathHubDto dto)
  {
    Console.WriteLine($"Received validate file path request for {dto.FileName} in {dto.DirectoryPath}");
    return Task.FromResult(new ValidateFilePathResponseDto(true));
  }

  public Task<PathSegmentsResponseDto> GetPathSegments(GetPathSegmentsHubDto dto)
  {
    Console.WriteLine($"Received get path segments request for {dto.TargetPath}");
    return Task.FromResult(new PathSegmentsResponseDto
    {
      Success = true,
      PathExists = true,
      PathSegments = ["C:", "Users", "TestUser", "Documents"]
    });
  }
}
