using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Enums;
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
      new("C:", "C:\\", true, 0, DateTimeOffset.Now, false, true, true)
    };
    return Task.FromResult(Result.Ok(new GetRootDrivesResponseDto(drives)));
  }

  public Task<Result<GetSubdirectoriesResponseDto>> GetSubdirectories(GetSubdirectoriesRequestDto requestDto)
  {
    Console.WriteLine($"Getting subdirectories for {requestDto.DirectoryPath}");
    return Task.FromResult(Result.Ok(new GetSubdirectoriesResponseDto([])));
  }

  public Task<Result<GetDirectoryContentsResponseDto>> GetDirectoryContents(GetDirectoryContentsRequestDto requestDto)
  {
    Console.WriteLine($"Getting directory contents for {requestDto.DirectoryPath}");
    return Task.FromResult(Result.Ok(new GetDirectoryContentsResponseDto([])));
  }
}
