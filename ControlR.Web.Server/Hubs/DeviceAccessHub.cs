using System.Threading.Channels;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Shared.Hubs.Clients;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Hubs;

public class DeviceAccessHub(
  UserManager<AppUser> userManager,
  AppDb appDb,
  IAuthorizationService authorizationService,
  IHubStreamStore hubStreamStore,
  IHubContext<AgentHub, IAgentHubClient> agentHub,
  IOptionsMonitor<AppOptions> appOptions,
  ILogger<DeviceAccessHub> logger) 
  : BrowserHubBase<IDeviceAccessHubClient>(userManager, appDb, authorizationService, agentHub, logger), IDeviceAccessHub
{
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly IHubStreamStore _hubStreamStore = hubStreamStore;

  public async Task<Result> CloseChatSession(Guid deviceId, Guid sessionId, int targetProcessId)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return Result.Fail("Unauthorized.");
      }

      Logger.LogInformation(
        "Closing chat session {SessionId} for device {DeviceId} and process {ProcessId}",
        sessionId,
        deviceId,
        targetProcessId);

      return await AgentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .CloseChatSession(sessionId, targetProcessId);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while closing chat session {SessionId} on device {DeviceId}.", sessionId, deviceId);
      return Result.Fail("Agent could not be reached.");
    }
  }

  public async Task CloseTerminalSession(Guid deviceId, Guid terminalSessionId)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return;
      }
      await AgentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .CloseTerminalSession(terminalSessionId);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while closing terminal session.");
    }
  }

  public async Task<Result> CreateTerminalSession(
    Guid deviceId,
    Guid terminalSessionId)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return Result.Fail("Forbidden.");
      }

      return await AgentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .CreateTerminalSession(terminalSessionId, Context.ConnectionId);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while creating terminal session.");
      return Result.Fail("An error occurred.");
    }
  }

  public async Task<DesktopSession[]> GetActiveDesktopSessions(Guid deviceId)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return [];
      }

      var device = authResult.Value;
      return await AgentHub.Clients.Client(device.ConnectionId).GetActiveUiSessions();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while getting Windows sessions from agent.");
      return [];
    }
  }

  public async Task<Result<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto request)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(request.DeviceId) is not { IsSuccess: true } authResult)
      {
        return Result.Fail<PwshCompletionsResponseDto>("Forbidden.");
      }

      // Create a new request with ViewerConnectionId
      var requestWithViewerConnection = request with { ViewerConnectionId = Context.ConnectionId };

      return await AgentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .GetPwshCompletions(requestWithViewerConnection);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while getting PowerShell command completions.");
      return Result.Fail<PwshCompletionsResponseDto>("An error occurred.");
    }
  }
  

  public async Task<Result> RequestStreamingSession(
    Guid deviceId,
    RemoteControlSessionRequestDto sessionRequestDto)
  {
    try
    {
      if (Context.User is null)
      {
        return Result.Fail("User is null.");
      }

      if (!TryGetUserId(out var userId))
      {
        return Result.Fail("Failed to get user ID.");
      }

      var user = await UserManager.Users
        .AsNoTracking()
        .Include(x => x.UserPreferences)
        .FirstOrDefaultAsync(x => x.Id == userId);

      if (user is null)
      {
        return Result.Fail("User not found.");
      }

      var displayName = user.UserPreferences
        ?.FirstOrDefault(x => x.Name == UserPreferenceNames.UserDisplayName)
        ?.Value;

      if (string.IsNullOrWhiteSpace(displayName))
      {
        displayName = user.UserName ?? "";
      }

      var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();

      Logger.LogInformation(
        "Starting streaming session requested by user {DisplayName} ({UserId}) for device {DeviceId} from IP {RemoteIp}.",
        displayName,
        userId,
        deviceId,
        remoteIp);

      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return Result.Fail("Unauthorized.");
      }

      var device = authResult.Value;

      var notifyUserSetting =
        AppDb.TenantSettings.FirstOrDefault(x => x.Name == TenantSettingsNames.NotifyUserOnSessionStart);
      if (notifyUserSetting is not null &&
          bool.TryParse(notifyUserSetting.Value, out var notifyUser))
      {
        sessionRequestDto = sessionRequestDto with { NotifyUserOnSessionStart = notifyUser };
      }

      sessionRequestDto = sessionRequestDto with { ViewerName = displayName };

      return await AgentHub.Clients
        .Client(device.ConnectionId)
        .CreateRemoteControlSession(sessionRequestDto);
    }
    catch (Exception ex)
    {
      return Result.Fail(ex);
    }
  }

  public async Task<Result> SendChatMessage(Guid deviceId, ChatMessageHubDto dto)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return Result.Fail("Unauthorized.");
      }

      // Log the chat message being sent
      Logger.LogInformation(
        "Chat message sent by user {SenderName} ({SenderEmail}) to device {DeviceId} for session {SessionId}",
        dto.SenderName,
        dto.SenderEmail,
        deviceId,
        dto.SessionId);

      var user = await GetRequiredUser(q => q.Include(u => u.UserPreferences));
      var displayName = await GetDisplayName(user);
      dto = dto with
      {
        ViewerConnectionId = Context.ConnectionId,
        SenderName = displayName,
        SenderEmail = $"{user.Email}"
      };

      return await AgentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .SendChatMessage(dto);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending chat message to agent.");
      return Result.Fail("Agent could not be reached.");
    }
  }

  public async Task<Result> SendTerminalInput(Guid deviceId, TerminalInputDto dto)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return Result.Fail("Unauthorized.");
      }

      // Create a new DTO with ViewerConnectionId
      var dtoWithViewerConnection = dto with { ViewerConnectionId = Context.ConnectionId };

      return await AgentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .ReceiveTerminalInput(dtoWithViewerConnection);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while sending terminal input.");
      return Result.Fail("Agent could not be reached.");
    }
  }

  public async Task<Result> UploadFile(
    FileUploadMetadata fileUploadMetadata,
    ChannelReader<byte[]> fileStream)
  {
    try
    {
      var deviceId = fileUploadMetadata.DeviceId;

      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return Result.Fail("Unauthorized.");
      }

      var maxUploadSize = _appOptions.CurrentValue.MaxFileTransferSize;
      if (maxUploadSize > 0 && fileUploadMetadata.FileSize > maxUploadSize)
      {
        return Result.Fail($"File size exceeds the maximum allowed size of {maxUploadSize} bytes.");
      }

      var device = authResult.Value;
      if (string.IsNullOrWhiteSpace(device.ConnectionId))
      {
        Logger.LogWarning("Device {DeviceId} is not connected (no ConnectionId).", deviceId);
        return Result.Fail("Device is not currently connected.");
      }

      var streamId = Guid.NewGuid();
      using var signaler = _hubStreamStore.GetOrCreate<byte[]>(streamId, TimeSpan.FromMinutes(30));

      var uploadRequest = new FileUploadHubDto(
        streamId,
        fileUploadMetadata.TargetDirectory,
        fileUploadMetadata.FileName,
        fileUploadMetadata.FileSize,
        fileUploadMetadata.Overwrite);

      // Asynchronously write the client's stream to the channel.
      var writeTask = signaler.WriteFromChannelReader(fileStream, Context.ConnectionAborted);

      // Notify the agent about the incoming upload
      var receiveResult = await AgentHub.Clients
        .Client(device.ConnectionId)
        .DownloadFileFromViewer(uploadRequest)
        .WaitAsync(Context.ConnectionAborted);

      if (receiveResult is null || !receiveResult.IsSuccess)
      {
        var reason = receiveResult?.Reason ?? "Agent did not respond.";
        Logger.LogWarning("Device {DeviceId} failed to download file {FileName}.  Reason: {Reason}",
          deviceId,
          fileUploadMetadata.FileName,
          reason);
        return Result.Fail($"Agent failed to download file: {reason}");
      }

      // Await the write task to ensure all data is sent or an error occurs.
      try
      {
        await writeTask;
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Error writing file stream for {FileName} to device {DeviceId}",
          fileUploadMetadata.FileName, fileUploadMetadata.DeviceId);
        return Result.Fail("An error occurred while writing the file stream.");
      }

      return Result.Ok();
    }
    catch (OperationCanceledException)
    {
      Logger.LogInformation("File upload was canceled by the user for file {FileName} to device {DeviceId}",
        fileUploadMetadata.FileName,
        fileUploadMetadata.DeviceId);
      return Result.Fail("File upload was canceled.");
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error uploading file {FileName} to device {DeviceId}",
        fileUploadMetadata.FileName, fileUploadMetadata.DeviceId);
      return Result.Fail("An error occurred during file upload.");
    }
  }

}