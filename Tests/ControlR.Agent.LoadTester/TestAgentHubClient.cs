using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Agent.LoadTester;

public class TestAgentHubClient : IAgentHubClient
{
  public Task<HubResult> CloseChatSession(Guid sessionId, int targetProcessId)
  {
    Console.WriteLine($"Closing chat session {sessionId} for process {targetProcessId}");
    return Task.FromResult(HubResult.Ok());
  }

  public Task CloseTerminalSession(Guid terminalSessionId)
  {
    Console.WriteLine($"Closing terminal session {terminalSessionId}");
    return Task.CompletedTask;
  }

  public Task<HubResult> CreateDirectory(CreateDirectoryHubDto dto)
  {
    Console.WriteLine($"Received create directory request for {dto.DirectoryName} in {dto.ParentPath}");
    return HubResult.Ok().AsTaskResult();
  }

  public Task<HubResult> CreateRemoteControlSession(RemoteControlSessionRequestDto dto)
  {
    Console.WriteLine($"Creating streaming session with ID: {dto.SessionId}, Viewer: {dto.ViewerName}");
    return Task.FromResult(HubResult.Ok());
  }

  public Task<HubResult> CreateTerminalSession(Guid terminalSessionId, string viewerConnectionId)
  {
    Console.WriteLine("Received terminal session request.");
    return HubResult.Ok().AsTaskResult();
  }

  public Task<HubResult> CreateVncSession(VncSessionRequestDto sessionRequestDto)
  {
    return HubResult.Ok().AsTaskResult();
  }

  public Task<HubResult> DeleteFile(FileDeleteHubDto dto)
  {
    Console.WriteLine($"Received file delete request for {dto.TargetPath}");
    return HubResult.Ok().AsTaskResult();
  }

  public Task<HubResult> DownloadFileFromViewer(FileUploadHubDto dto)
  {
    Console.WriteLine($"Received file upload request for {dto.FileName} to {dto.TargetDirectoryPath}");
    return HubResult.Ok().AsTaskResult();
  }

  public Task<DesktopSession[]> GetActiveDesktopSessions()
  {
    var session = new DesktopSession
    {
      SystemSessionId = 1,
      Name = "Console",
      Type = DesktopSessionType.Console,
      Username = "TestUser"
    };
    return Task.FromResult(new[] { session });
  }

  public Task<HubResult<GetLogFilesResponseDto>> GetLogFiles()
  {
    Console.WriteLine("Getting log files");
    var responseDto = new GetLogFilesResponseDto(LogFileGroups: []);
    return HubResult.Ok(responseDto).AsTaskResult();
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

  public Task<HubResult<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto request)
  {
    return Task.FromResult(HubResult.Ok(new PwshCompletionsResponseDto(
      ReplacementIndex: 0,
      ReplacementLength: 0,
      CompletionMatches: [],
      HasMorePages: false,
      TotalCount: 0,
      CurrentPage: 0
    )));
  }

  public Task<HubResult<GetRootDrivesResponseDto>> GetRootDrives(GetRootDrivesRequestDto requestDto)
  {
    Console.WriteLine($"Getting root drives for device {requestDto.DeviceId}");
    var drives = new FileSystemEntryDto[]
    {
      new("C:", "C:\\", true, 0, DateTimeOffset.Now, false, true, true, true)
    };
    return Task.FromResult(HubResult.Ok(new GetRootDrivesResponseDto(drives)));
  }

  public Task<HubResult> InvokeCtrlAltDel(InvokeCtrlAltDelRequestDto requestDto)
  {
    Console.WriteLine("Received Ctrl+Alt+Del request.");
    return Task.FromResult(HubResult.Ok());
  }

  public Task InvokeWakeDevice(WakeDeviceDto dto)
  {
    Console.WriteLine("Received wake device request.");
    return Task.CompletedTask;
  }

  public Task ReceiveAgentUpdateTrigger()
  {
    Console.WriteLine("Received agent update trigger.");
    return Task.CompletedTask;
  }

  public Task ReceiveDto(DtoWrapper dtoWrapper)
  {
    Console.WriteLine($"Received DTO of type: {dtoWrapper.DtoType}");
    return Task.CompletedTask;
  }

  public Task ReceivePowerStateChange(PowerStateChangeType changeType)
  {
    Console.WriteLine($"Received power state change: {changeType}");
    return Task.CompletedTask;
  }

  public Task<HubResult> ReceiveTerminalInput(TerminalInputDto dto)
  {
    Console.WriteLine($"Received terminal input: {dto.Input}");
    return Task.FromResult(HubResult.Ok());
  }

  public Task RefreshDeviceInfo()
  {
    Console.WriteLine("Refreshing device info.");
    return Task.CompletedTask;
  }

  public Task<HubResult> RequestDesktopPreview(DesktopPreviewRequestDto dto)
  {
    Console.WriteLine($"Desktop preview requested. Requester: {dto.RequesterId}, Stream: {dto.StreamId}, Process: {dto.TargetProcessId}");
    return Task.FromResult(HubResult.Ok());
  }

  public Task<HubResult> SendChatMessage(ChatMessageHubDto dto)
  {
    Console.WriteLine($"Sending chat message from {dto.SenderName} ({dto.SenderEmail}): {dto.Message}");
    return Task.FromResult(HubResult.Ok());
  }

  public Task<HubResult> StreamDirectoryContents(DirectoryContentsStreamRequestHubDto dto)
  {
    Console.WriteLine($"Streaming directory contents for {dto.DirectoryPath} (stream {dto.StreamId})");
    return HubResult.Ok().AsTaskResult();
  }

  public Task<HubResult> StreamFileContents(StreamFileContentsRequestHubDto dto)
  {
    Console.WriteLine($"Streaming log file contents for {dto.FilePath} (stream {dto.StreamId})");
    return HubResult.Ok().AsTaskResult();
  }

  public Task<HubResult> StreamSubdirectories(SubdirectoriesStreamRequestHubDto dto)
  {
    Console.WriteLine($"Streaming subdirectories for {dto.DirectoryPath} (stream {dto.StreamId})");
    return HubResult.Ok().AsTaskResult();
  }

  public Task<HubResult> TestVncConnection(int port)
  {
    return HubResult.Ok().AsTaskResult();
  }

  public Task UninstallAgent(string reason)
  {
    Console.WriteLine($"Uninstalling agent for reason: {reason}");
    return Task.CompletedTask;
  }

  public Task<HubResult<FileDownloadResponseHubDto>> UploadFileToViewer(FileDownloadHubDto dto)
  {
    Console.WriteLine($"Received file download request for {dto.FilePath}");
    return HubResult.Ok(new FileDownloadResponseHubDto(FileSize: 0, FileDisplayName: "Test.zip")).AsTaskResult();
  }

  public Task<ValidateFilePathResponseDto> ValidateFilePath(ValidateFilePathHubDto dto)
  {
    Console.WriteLine($"Received validate file path request for {dto.FileName} in {dto.DirectoryPath}");
    return Task.FromResult(new ValidateFilePathResponseDto(true));
  }
}

