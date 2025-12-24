using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Hubs.Clients;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Hubs;

[Authorize]
public class ViewerHub(
  UserManager<AppUser> userManager,
  AppDb appDb,
  IAuthorizationService authorizationService,
  IHubContext<AgentHub, IAgentHubClient> agentHub,
  IHubStreamStore hubStreamStore,
  IOptionsMonitor<AppOptions> appOptions,
  ILogger<ViewerHub> logger)
  : HubWithItems<IViewerHubClient>, IViewerHub
{
  private readonly IHubContext<AgentHub, IAgentHubClient> _agentHub = agentHub;
  private readonly AppDb _appDb = appDb;
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly IAuthorizationService _authorizationService = authorizationService;
  private readonly IHubStreamStore _hubStreamStore = hubStreamStore;
  private readonly ILogger<ViewerHub> _logger = logger;
  private readonly UserManager<AppUser> _userManager = userManager;

  public async Task<Result> CloseChatSession(Guid deviceId, Guid sessionId, int targetProcessId)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return Result.Fail("Unauthorized.");
      }

      _logger.LogInformation(
        "Closing chat session {SessionId} for device {DeviceId} and process {ProcessId}",
        sessionId,
        deviceId,
        targetProcessId);

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .CloseChatSession(sessionId, targetProcessId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while closing chat session {SessionId} on device {DeviceId}.", sessionId, deviceId);
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

      await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .CloseTerminalSession(terminalSessionId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while closing terminal session.");
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

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .CreateTerminalSession(terminalSessionId, Context.ConnectionId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating terminal session.");
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
      return await _agentHub.Clients.Client(device.ConnectionId).GetActiveDesktopSessions();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting Windows sessions from agent.");
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

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .GetPwshCompletions(requestWithViewerConnection);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting PowerShell command completions.");
      return Result.Fail<PwshCompletionsResponseDto>("An error occurred.");
    }
  }

  public async Task<Result> InvokeCtrlAltDel(Guid deviceId, int targetDesktopProcessId, DesktopSessionType desktopSessionType)
  {
    try
    {
      _logger.LogInformation(
        "Invoking CtrlAltDel for device {DeviceId} and process {ProcessId}.  User: {UserId}", 
        deviceId,
        targetDesktopProcessId,
        Context.UserIdentifier);

      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return Result.Fail("Unauthorized.");
      }

      if (!TryGetUserId(out var userId))
      {
        _logger.LogError("Failed to get user ID for CtrlAltDel invocation.");
        return Result.Fail("Failed to get user ID.");
      }

      var displayNameResult = await GetDisplayName(userId);
      if (!displayNameResult.IsSuccess)
      {
        return displayNameResult.ToResult();
      }

      var dto = new InvokeCtrlAltDelRequestDto(
        targetDesktopProcessId, 
        Context.User?.Identity?.Name ?? "Unknown",
        desktopSessionType);

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .InvokeCtrlAltDel(dto);
    }
    catch (Exception ex)
    {
      return Result
        .Fail(ex, "An error occurred while invoking CtrlAltDel.")
        .Log(_logger);
    }
  }

  public override async Task OnConnectedAsync()
  {
    try
    {
      await base.OnConnectedAsync();

      if (Context.User?.TryGetUserId(out var userId) != true)
      {
        _logger.LogCritical("User is null.  Authorize tag should have prevented this.");
        return;
      }

      if (!Context.User.TryGetTenantId(out var tenantId))
      {
        _logger.LogCritical("Failed to get tenant ID.");
        return;
      }

      var user = await _appDb.Users
        .Include(x => x.Tags)
        .FirstOrDefaultAsync(x => x.Id == userId);

      if (user is null)
      {
        _logger.LogCritical("Failed to find user from UserManager.");
        return;
      }

      user.IsOnline = true;
      await _appDb.SaveChangesAsync();

      if (Context.User.IsInRole(RoleNames.ServerAdministrator))
      {
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.ServerAdministrators);
      }

      if (Context.User.IsInRole(RoleNames.TenantAdministrator))
      {
        await Groups.AddToGroupAsync(Context.ConnectionId,
          HubGroupNames.GetUserRoleGroupName(RoleNames.TenantAdministrator, tenantId));
      }

      if (Context.User.IsInRole(RoleNames.DeviceSuperUser))
      {
        await Groups.AddToGroupAsync(Context.ConnectionId,
          HubGroupNames.GetUserRoleGroupName(RoleNames.DeviceSuperUser, tenantId));
      }

      if (user.Tags is { Count: > 0 } tags)
      {
        foreach (var tag in tags)
        {
          await Groups.AddToGroupAsync(Context.ConnectionId,
            HubGroupNames.GetTagGroupName(tag.Id, tenantId));
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during viewer connect.");
    }
  }

  public override async Task OnDisconnectedAsync(Exception? exception)
  {
    try
    {
      await base.OnDisconnectedAsync(exception);

      if (Context.User is null)
      {
        return;
      }

      var user = await _userManager.GetUserAsync(Context.User);

      if (user is null)
      {
        _logger.LogCritical("Failed to find user from UserManager.");
        return;
      }

      user.IsOnline = false;
      await _userManager.UpdateAsync(user);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during viewer disconnect.");
    }
  }

  public async Task RefreshDeviceInfo(Guid deviceId)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return;
      }

      await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .RefreshDeviceInfo();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while refreshing device info.");
    }
  }

  public async Task<Result> RequestRemoteControlSession(
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

      var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();

      var displayNameResult = await GetDisplayName(userId);
      if (!displayNameResult.IsSuccess)
      {
        return displayNameResult.ToResult();
      }

      var displayName = displayNameResult.Value;

      _logger.LogInformation(
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
        _appDb.TenantSettings.FirstOrDefault(x => x.Name == TenantSettingsNames.NotifyUserOnSessionStart);
      if (notifyUserSetting is not null &&
          bool.TryParse(notifyUserSetting.Value, out var notifyUser))
      {
        sessionRequestDto = sessionRequestDto with { NotifyUserOnSessionStart = notifyUser };
      }

      sessionRequestDto = sessionRequestDto with
      {
        ViewerName = displayName,
        ViewerConnectionId = Context.ConnectionId
      };

      return await _agentHub.Clients
        .Client(device.ConnectionId)
        .CreateRemoteControlSession(sessionRequestDto);
    }
    catch (Exception ex)
    {
      return Result.Fail(ex);
    }
  }

  public async Task<Result> RequestVncSession(Guid deviceId, VncSessionRequestDto sessionRequestDto)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return Result.Fail("Unauthorized.");
      }

      if (Context.User is null)
      {
        return Result.Fail("User is null.");
      }

      if (!TryGetUserId(out var userId))
      {
        return Result.Fail("Failed to get user ID.");
      }

      var user = await _userManager.Users
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
      var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();

      _logger.LogInformation(
        "Starting VNC session requested by user {DisplayName} ({UserId}) for device {DeviceId} from IP {RemoteIp}.",
        displayName,
        userId,
        deviceId,
        remoteIp);

      var device = authResult.Value;

      if (string.IsNullOrWhiteSpace(displayName))
      {
        displayName = user.UserName ?? "";
      }

      sessionRequestDto = sessionRequestDto with 
      { 
        ViewerConnectionId = Context.ConnectionId,
        ViewerName = displayName,
      };

      return await _agentHub.Clients
        .Client(device.ConnectionId)
        .CreateVncSession(sessionRequestDto);
    }
    catch (Exception ex)
    {
      return Result.Fail(ex);
    }
  }

  public async Task SendAgentUpdateTrigger(Guid deviceId)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return;
      }

      await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .ReceiveAgentUpdateTrigger();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending agent update trigger.");
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

      var user = await GetRequiredUser(q => q.Include(u => u.UserPreferences));
      var displayName = await GetDisplayName(user);

      // Log the chat message being sent
      _logger.LogInformation(
        "Chat message sent by user {SenderName} ({SenderEmail}) to device {DeviceId} for session {SessionId}",
        displayName,
        user.Email,
        deviceId,
        dto.SessionId);

      dto = dto with
      {
        ViewerConnectionId = Context.ConnectionId,
        SenderName = displayName,
        SenderEmail = $"{user.Email}"
      };

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .SendChatMessage(dto);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending chat message to agent.");
      return Result.Fail("Agent could not be reached.");
    }
  }

  public async Task SendDtoToAgent(Guid deviceId, DtoWrapper wrapper)
  {
    try
    {
      using var scope = _logger.BeginMemberScope();

      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return;
      }

      await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .ReceiveDto(wrapper);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending DTO to agent.");
    }
  }

  public async Task SendDtoToUserGroups(DtoWrapper wrapper)
  {
    if (!TryGetUserId(out var userId) ||
        !TryGetTenantId(out var tenantId))
    {
      return;
    }

    if (Context.User!.IsInRole(RoleNames.DeviceSuperUser))
    {
      await _agentHub
        .Clients
        .Group(HubGroupNames.GetTenantDevicesGroupName(tenantId))
        .ReceiveDto(wrapper);
      return;
    }

    var user = await _userManager
      .Users
      .AsNoTracking()
      .Include(x => x.Tags!)
      .ThenInclude(x => x.Devices)
      .FirstOrDefaultAsync(x => x.Id == userId);

    if (user?.Tags is null)
    {
      return;
    }

    var groupNames = user.Tags.Select(x => HubGroupNames.GetTagGroupName(x.Id, x.TenantId));
    await _agentHub.Clients.Groups(groupNames).ReceiveDto(wrapper);
  }

  public async Task SendPowerStateChange(Guid deviceId, PowerStateChangeType changeType)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return;
      }

      await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .ReceivePowerStateChange(changeType);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending power state change.");
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

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .ReceiveTerminalInput(dtoWithViewerConnection);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending terminal input.");
      return Result.Fail("Agent could not be reached.");
    }
  }

  public async Task SendWakeDevice(Guid deviceId, string[] macAddresses)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return;
      }

      var tagsIds = await _appDb.Devices
        .Include(x => x.Tags)
        .Where(x => x.Id == deviceId)
        .SelectMany(x => x.Tags!)
        .Select(x => x.Id)
        .ToListAsync();

      var tagGroupNames = tagsIds.Select(tagId =>
        HubGroupNames.GetTagGroupName(tagId, authResult.Value.TenantId));

      var dto = new WakeDeviceDto(macAddresses);
      await _agentHub.Clients
        .Groups(tagGroupNames)
        .InvokeWakeDevice(dto);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending wake device command.");
    }
  }

  public async Task<Result> TestVncConnection(Guid guid, int port)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(guid) is not { IsSuccess: true } authResult)
      {
        return Result.Fail("Unauthorized.");
      }

      return await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .TestVncConnection(port);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while testing VNC connection.");
      return Result.Fail("An error occurred while testing the VNC connection.");
    }
  }

  public async Task UninstallAgent(Guid deviceId, string reason)
  {
    try
    {
      if (await TryAuthorizeAgainstDevice(deviceId) is not { IsSuccess: true } authResult)
      {
        return;
      }

      _logger.LogInformation(
        "Agent uninstall command sent by user: {UserName}.  Device: {DeviceId}",
        Context.UserIdentifier,
        deviceId);

      await _agentHub.Clients
        .Client(authResult.Value.ConnectionId)
        .UninstallAgent(reason);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while uninstalling agent.");
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
        _logger.LogWarning("Device {DeviceId} is not connected (no ConnectionId).", deviceId);
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
      var receiveResult = await _agentHub.Clients
        .Client(device.ConnectionId)
        .DownloadFileFromViewer(uploadRequest)
        .WaitAsync(Context.ConnectionAborted);

      if (receiveResult is null || !receiveResult.IsSuccess)
      {
        var reason = receiveResult?.Reason ?? "Agent did not respond.";
        _logger.LogWarning("Device {DeviceId} failed to download file {FileName}.  Reason: {Reason}",
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
        _logger.LogError(ex, "Error writing file stream for {FileName} to device {DeviceId}",
          fileUploadMetadata.FileName, fileUploadMetadata.DeviceId);
        return Result.Fail("An error occurred while writing the file stream.");
      }

      return Result.Ok();
    }
    catch (OperationCanceledException)
    {
      _logger.LogInformation("File upload was canceled by the user for file {FileName} to device {DeviceId}",
        fileUploadMetadata.FileName,
        fileUploadMetadata.DeviceId);
      return Result.Fail("File upload was canceled.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error uploading file {FileName} to device {DeviceId}",
        fileUploadMetadata.FileName, fileUploadMetadata.DeviceId);
      return Result.Fail("An error occurred during file upload.");
    }
  }

  private static Task<string> GetDisplayName(AppUser user, string fallbackName = "Admin")
  {
    var displayName = user.UserPreferences
      ?.FirstOrDefault(x => x.Name == UserPreferenceNames.UserDisplayName)
      ?.Value;

    if (string.IsNullOrWhiteSpace(displayName))
    {
      displayName = user.UserName ?? fallbackName;
    }

    return displayName.AsTaskResult();
  }

  private async Task<Result<string>> GetDisplayName(Guid userId)
  {
    var user = await _userManager.Users
      .AsNoTracking()
      .Include(x => x.UserPreferences)
      .FirstOrDefaultAsync(x => x.Id == userId);

    if (user is null)
    {
      return Result
        .Fail<string>("User not found.")
        .Log(_logger);
    }

    var displayName = user.UserPreferences
      ?.FirstOrDefault(x => x.Name == UserPreferenceNames.UserDisplayName)
      ?.Value;

    if (string.IsNullOrWhiteSpace(displayName))
    {
      displayName = user.UserName ?? "";
    }
    return Result.Ok(displayName);
  }

  private async Task<AppUser> GetRequiredUser(Func<IQueryable<AppUser>, IQueryable<AppUser>>? includeBuilder = null)
  {
    if (!TryGetUserId(out var userId))
    {
      throw new UnauthorizedAccessException("Failed to get user ID.");
    }

    var query = _userManager.Users.AsNoTracking();

    if (includeBuilder is not null)
    {
      query = includeBuilder.Invoke(query);
    }

    var user = await query.FirstOrDefaultAsync(x => x.Id == userId);

    Guard.IsNotNull(user);
    return user;
  }

  private bool IsServerAdmin()
  {
    return Context.User?.IsInRole(RoleNames.ServerAdministrator) ?? false;
  }

  private async Task<Result<Device>> TryAuthorizeAgainstDevice(
    Guid deviceId,
    [CallerMemberName] string? callerName = null)
  {
    if (Context.User is null)
    {
      _logger.LogCritical("User is null.  Authorize tag should have prevented this.");
      return Result.Fail<Device>("User is null.  Authorize tag should have prevented this.");
    }

    var device = await _appDb.Devices
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == deviceId);

    if (device is null)
    {
      _logger.LogWarning("Device {DeviceId} not found.", deviceId);
      return Result.Fail<Device>("Device not found.");
    }

    var authResult = await _authorizationService.AuthorizeAsync(
      Context.User,
      device,
      DeviceAccessByDeviceResourcePolicy.PolicyName);

    if (authResult.Succeeded)
    {
      return Result.Ok(device);
    }

    _logger.LogCritical(
      "Unauthorized agent access attempted by user: {UserName}.  Device: {DeviceId}.  Method: {MemberName}.",
      Context.UserIdentifier,
      deviceId,
      callerName);

    return Result.Fail<Device>("Unauthorized.");
  }

  private bool TryGetTenantId(
    out Guid tenantId,
    [CallerMemberName] string callerName = "")
  {
    tenantId = Guid.Empty;
    if (Context.User?.TryGetTenantId(out tenantId) == true)
    {
      return true;
    }

    _logger.LogError("TenantId claim is unexpected missing when calling {MemberName}.", callerName);
    return false;
  }

  private bool TryGetUserId(
    out Guid userId,
    [CallerMemberName] string callerName = "")
  {
    userId = Guid.Empty;
    if (Context.User?.TryGetUserId(out userId) == true)
    {
      return true;
    }

    _logger.LogError("UserId claim is unexpected missing when calling {MemberName}.", callerName);
    return false;
  }

  private bool VerifyIsServerAdmin([CallerMemberName] string callerMember = "")
  {
    if (IsServerAdmin())
    {
      return true;
    }

    var userName = Context.User?.Identity?.Name;
    _logger.LogCritical(
      "Admin verification failed when invoking member {MemberName}. User: {UserName}",
      callerMember,
      userName);

    return false;
  }
}