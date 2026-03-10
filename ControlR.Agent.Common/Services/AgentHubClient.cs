using System.Diagnostics;
using System.Threading.Channels;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Models.Messages;
using ControlR.Agent.Common.Services.FileManager;
using ControlR.Agent.Common.Services.Terminal;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Libraries.Signalr.Client.Extensions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using ControlR.Libraries.Shared.Services.Processes;
using ControlR.Libraries.Shared.Services.FileSystem;

namespace ControlR.Agent.Common.Services;

// ReSharper disable once ClassNeverInstantiated.GLobal
internal class AgentHubClient(
  IHubConnection<IAgentHub> hubConnection,
  ISystemEnvironment systemEnvironment,
  IMessenger messenger,
  ITerminalStore terminalStore,
  IDesktopSessionProvider desktopSessionProvider,
  IIpcServerStore ipcServerStore,
  IDesktopClientUpdater streamerUpdater,
  IHostApplicationLifetime appLifetime,
  ISettingsProvider settings,
  IProcessManager processManager,
  IPowerControl powerControl,
  ILocalSocketProxy localProxy,
  IFileManager fileManager,
  IFileSystem fileSystem,
  IDeviceInfoProvider deviceDataGenerator,
  IAgentUpdater agentUpdater,
  IWakeOnLanService wakeOnLan,
  IAgentHeartbeatTimer heartbeatTimer,
  ILogger<AgentHubClient> logger) : IAgentHubClient
{
  private readonly IAgentUpdater _agentUpdater = agentUpdater;
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly IDesktopClientUpdater _desktopClientUpdater = streamerUpdater;
  private readonly IDesktopSessionProvider _desktopSessionProvider = desktopSessionProvider;
  private readonly IDeviceInfoProvider _deviceDataGenerator = deviceDataGenerator;
  private readonly IFileManager _fileManager = fileManager;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IAgentHeartbeatTimer _heartbeatTimer = heartbeatTimer;
  private readonly IHubConnection<IAgentHub> _hubConnection = hubConnection;
  private readonly IIpcServerStore _ipcServerStore = ipcServerStore;
  private readonly ILocalSocketProxy _localProxy = localProxy;
  private readonly ILogger<AgentHubClient> _logger = logger;
  private readonly IMessenger _messenger = messenger;
  private readonly IPowerControl _powerControl = powerControl;
  private readonly IProcessManager _processManager = processManager;
  private readonly ISettingsProvider _settings = settings;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly ITerminalStore _terminalStore = terminalStore;
  private readonly IWakeOnLanService _wakeOnLan = wakeOnLan;

  public async Task<HubResult> CloseChatSession(Guid sessionId, int targetProcessId)
  {
    try
    {
      _logger.LogInformation(
        "Closing chat session {SessionId} for process ID {ProcessId}",
        sessionId,
        targetProcessId);

      if (!_ipcServerStore.TryGetServer(targetProcessId, out var ipcServer))
      {
        _logger.LogWarning(
          "No IPC server found for process ID {ProcessId}. Cannot close chat session.",
          targetProcessId);
        return HubResult.Fail("IPC server not found for target process.");
      }

      var ipcDto = new CloseChatSessionIpcDto(sessionId, targetProcessId);
      await ipcServer.Server.Client.CloseChatSession(ipcDto);

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while closing chat session {SessionId}.", sessionId);
      return HubResult.Fail("An error occurred while closing chat session.");
    }
  }

  public Task CloseTerminalSession(Guid terminalSessionId)
  {
    try
    {
      _logger.LogInformation("Closing terminal session {TerminalSessionId}.", terminalSessionId);

      // The pwsh resources are disposed upon eviction from the MemoryCache.
      _ = _terminalStore.TryRemove(terminalSessionId, out _);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while closing terminal session {TerminalSessionId}.", terminalSessionId);
    }

    return Task.CompletedTask;
  }

  public async Task<HubResult> CreateDirectory(CreateDirectoryHubDto dto)
  {
    try
    {
      _logger.LogInformation("Creating directory: {DirectoryName} in {ParentPath}", dto.DirectoryName, dto.ParentPath);

      var result = await _fileManager.CreateDirectory(dto.ParentPath, dto.DirectoryName);

      if (result.IsSuccess)
      {
        _logger.LogInformation("Successfully created directory: {DirectoryName} in {ParentPath}", dto.DirectoryName,
          dto.ParentPath);
        return HubResult.Ok();
      }

      _logger.LogWarning("Failed to create directory: {DirectoryName} in {ParentPath}, Error: {Error}",
        dto.DirectoryName, dto.ParentPath, result.ErrorMessage);
      return HubResult.Fail(result.ErrorMessage ?? "Failed to create directory");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating directory: {DirectoryName} in {ParentPath}", dto.DirectoryName,
        dto.ParentPath);
      return HubResult.Fail("An error occurred while creating directory.");
    }
  }

  public async Task<HubResult> CreateRemoteControlSession(RemoteControlSessionRequestDto dto)
  {
    try
    {
      if (!_settings.DisableAutoUpdate)
      {
        var versionResult = await _desktopClientUpdater.EnsureLatestVersion(
          acquireGlobalLock: true,
          _appLifetime.ApplicationStopping);

        if (!versionResult)
        {
          return HubResult.Fail("Failed to ensure latest desktop client version is installed.");
        }
      }

      _logger.LogInformation(
        "Creating remote control session.  Session ID: {SessionId}, Viewer Connection ID: {ViewerConnectionId}, " +
        "Target System Session: {TargetSystemSession}, Process ID: {TargetProcessId}",
        dto.SessionId,
        dto.ViewerConnectionId,
        dto.TargetSystemSession,
        dto.TargetProcessId);

      if (!_ipcServerStore.TryGetServer(dto.TargetProcessId, out var ipcServer))
      {
        _logger.LogWarning(
          "No IPC server found for process ID {ProcessId}.  Cannot create remote control session.",
          dto.TargetProcessId);
        return HubResult.Fail($"Process with ID {dto.TargetProcessId} is no longer running.");
      }

      var dataFolder = string.IsNullOrWhiteSpace(_settings.InstanceId)
        ? "Default"
        : _settings.InstanceId;

      var ipcDto = new RemoteControlRequestIpcDto(
        dto.SessionId,
        dto.WebsocketUri,
        dto.TargetSystemSession,
        dto.TargetProcessId,
        dto.DeviceId,
        dto.NotifyUserOnSessionStart,
        dto.RequireConsent,
        dataFolder,
        dto.ViewerConnectionId,
        dto.ViewerName);

      var result = await ipcServer.Server.Client.ReceiveRemoteControlRequest(ipcDto);
      _logger.LogInformation(
        "Remote control session created successfully for process ID {ProcessId}.",
        dto.TargetProcessId);

      if (result.IsSuccess)
      {
        _logger.LogInformation(
          "Remote control session established on target process {ProcessId}.",
          dto.TargetProcessId);
      }
      else
      {
        _logger.LogError(
          "Failed to create remote control session on target process {ProcessId}. Reason: {Error}",
          dto.TargetProcessId,
          result.Reason ?? "Unknown error");
      }

      return result.ToHubResult();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating remote control session.");
      return HubResult.Fail("An error occurred on the agent while creating remote control session.");
    }
  }

  public async Task<HubResult> CreateTerminalSession(Guid terminalSessionId, string viewerConnectionId)
  {
    try
    {
      _logger.LogInformation("Terminal session started.  Viewer Connection ID: {ConnectionId}",
        viewerConnectionId);

      return (await _terminalStore.CreateSession(terminalSessionId, viewerConnectionId)).ToHubResult();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating terminal session.");
      return HubResult.Fail("An error occurred.");
    }
  }

  public async Task<HubResult> CreateVncSession(VncSessionRequestDto sessionRequestDto)
  {
    try
    {
      _logger.LogInformation(
        "VNC session requested.  Viewer Connection ID: {ConnectionId}.",
        sessionRequestDto.ViewerConnectionId);

      return (await _localProxy.HandleVncSession(sessionRequestDto).ConfigureAwait(false)).ToHubResult();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating VNC session.");
      return HubResult.Fail("An error occurred while creating VNC session.");
    }
  }

  public async Task<HubResult> DeleteFile(FileDeleteHubDto dto)
  {
    try
    {
      _logger.LogInformation("Delete file system entry: {FilePath}", dto.TargetPath);

      var result = await _fileManager.DeleteFileSystemEntry(dto.TargetPath);

      if (result.IsSuccess)
      {
        _logger.LogInformation("Successfully deleted file system entry: {FilePath}", dto.TargetPath);
        return HubResult.Ok();
      }

      _logger.LogWarning("Failed to delete file system entry: {FilePath}, Error: {Error}",
        dto.TargetPath, result.ErrorMessage);
      return HubResult.Fail(result.ErrorMessage ?? "Failed to delete file system entry");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while deleting file system entry: {FilePath}", dto.TargetPath);
      return HubResult.Fail("An error occurred while deleting file system entry.");
    }
  }

  public async Task<HubResult> DownloadFileFromViewer(FileUploadHubDto dto)
  {
    try
    {
      _logger.LogInformation("Downloading file from viewer: {FileName} to {Directory}",
        dto.FileName, dto.TargetDirectoryPath);

      var targetPath = Path.Join(dto.TargetDirectoryPath, dto.FileName);

      // Check if the file already exists and overwrite is not allowed
      if (_fileSystem.FileExists(targetPath) && !dto.Overwrite)
      {
        _logger.LogWarning("File already exists and overwrite is not allowed: {FilePath}", targetPath);
        return HubResult.Fail("File already exists.");
      }

      await using var fs = _fileSystem.OpenFileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
      var channelReader = _hubConnection.Server.GetFileStreamFromViewer(dto);
      await foreach (var chunk in channelReader.ReadAllAsync())
      {
        // Process each chunk (e.g., write to file, buffer, etc.)
        await fs.WriteAsync(chunk);
      }

      _logger.LogInformation(
        "Successfully downloaded file from viewer: {FileName} to {Directory}",
        dto.FileName,
        dto.TargetDirectoryPath);
      return HubResult.Ok();
    }
    catch (UnauthorizedAccessException ex)
    {
      _logger.LogError(ex, "Permission denied when downloading file from viewer: {FileName} to {Directory}",
        dto.FileName, dto.TargetDirectoryPath);
      return HubResult.Fail("Permission denied. Unable to write to the target directory.");
    }
    catch (IOException ex) when (ex.Message.EndsWith("used by another process."))
    {
      _logger.LogError(ex, "Unable to overwrite file downloaded from viewer: {FileName} to {Directory}",
        dto.FileName, dto.TargetDirectoryPath);
      return HubResult.Fail("File is in use. Unable to overwrite.");
    }
    catch (HubException ex) when (ex.Message.Contains("canceled by client"))
    {
      _logger.LogWarning("File upload from viewer was canceled by client: {FileName} to {Directory}",
        dto.FileName, dto.TargetDirectoryPath);
      return HubResult.Fail("File upload was canceled by client.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while downloading file from viewer: {FileName} to {Directory}",
        dto.FileName, dto.TargetDirectoryPath);
      return HubResult.Fail("An error occurred while downloading file from viewer.");
    }
  }

  public async Task<DesktopSession[]> GetActiveDesktopSessions()
  {
    return await _desktopSessionProvider.GetActiveDesktopClients();
  }

  public async Task<HubResult<GetLogFilesResponseDto>> GetLogFiles()
  {
    try
    {
      _logger.LogDebug("Getting log files");

      var logFileGroups = await _fileManager.GetLogFiles();

      var responseDto = new GetLogFilesResponseDto(logFileGroups);

      return HubResult.Ok(responseDto);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting log files");
      return HubResult.Fail<GetLogFilesResponseDto>("An error occurred while getting log files.");
    }
  }

  public async Task<PathSegmentsResponseDto> GetPathSegments(GetPathSegmentsHubDto dto)
  {
    try
    {
      _logger.LogDebug("Getting path segments for: {TargetPath}", dto.TargetPath);

      var result = await _fileManager.GetPathSegments(dto.TargetPath);

      _logger.LogDebug(
        "Path segments result - Success: {Success}, PathExists: {PathExists}, Segments: {SegmentCount}",
        result.Success, result.PathExists, result.PathSegments.Length);

      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting path segments for: {TargetPath}", dto.TargetPath);
      return new PathSegmentsResponseDto
      {
        Success = false,
        PathExists = false,
        PathSegments = [],
        ErrorMessage = "An error occurred while getting path segments."
      };
    }
  }

  public async Task<HubResult<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto request)
  {
    try
    {
      return (await _terminalStore.GetPwshCompletions(request)).ToHubResult();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting PowerShell completions.");
      return HubResult.Fail<PwshCompletionsResponseDto>("An error occurred while getting PowerShell completions.");
    }
  }

  public async Task<HubResult<GetRootDrivesResponseDto>> GetRootDrives(GetRootDrivesRequestDto requestDto)
  {
    try
    {
      _logger.LogDebug("Getting root drives");

      var drives = await _fileManager.GetRootDrives();

      return HubResult.Ok(new GetRootDrivesResponseDto(drives));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting root drives");
      return HubResult.Fail<GetRootDrivesResponseDto>("An error occurred while getting root drives.");
    }
  }

  public async Task<HubResult> InvokeCtrlAltDel(InvokeCtrlAltDelRequestDto dto)
  {
    try
    {
      _logger.LogInformation(
        "Invoke Ctrl+Alt+Del request received for target desktop process ID {TargetDesktopProcessId} from invoker {InvokerName}.",
        dto.TargetDesktopProcessId,
        dto.InvokerUserName);

      if (!OperatingSystem.IsWindows())
      {
        _logger.LogWarning("Ctrl+Alt+Del invocation is only supported on Windows.");
        return HubResult.Fail("Ctrl+Alt+Del invocation is only supported on Windows.");
      }

      if (dto.DesktopSessionType == DesktopSessionType.Rdp)
      {
        _logger.LogInformation(
          "Sending Ctrl+Alt+Del invocation to RDP desktop session with process ID {ProcessId}.",
          dto.TargetDesktopProcessId);

        if (_ipcServerStore.TryGetServer(dto.TargetDesktopProcessId, out var ipcServer))
        {
          await ipcServer.Server.Client.InvokeCtrlAltDel(dto);
          return HubResult.Ok();
        }
        else
        {
          _logger.LogWarning(
            "No IPC server found for process ID {ProcessId}. Cannot send Ctrl+Alt+Del invocation to desktop.",
            dto.TargetDesktopProcessId);

          return HubResult.Fail("IPC server not found for target desktop process.");
        }
      }

      await _messenger
        .SendEvent(EventKinds.CtrlAltDelInvoked)
        .ConfigureAwait(false);

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while invoking Ctrl+Alt+Del.");
      return HubResult.Fail("An error occurred while invoking Ctrl+Alt+Del.");
    }
  }

  public async Task InvokeWakeDevice(WakeDeviceDto dto)
  {
    try
    {
      await _wakeOnLan.WakeDevices(dto.MacAddresses);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while invoking wake device.");
    }
  }

  public async Task ReceiveAgentUpdateTrigger()
  {
    try
    {
      _agentUpdater.CheckForUpdate(force: true).Forget();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while receiving agent update trigger.");
    }
  }

  public Task ReceiveDto(DtoWrapper dto)
  {
    _messenger.Send(new DtoReceivedMessage<DtoWrapper>(dto)).Forget();
    return Task.CompletedTask;
  }

  public async Task ReceivePowerStateChange(PowerStateChangeType changeType)
  {
    await _powerControl.ChangeState(changeType);
  }

  public async Task<HubResult> ReceiveTerminalInput(TerminalInputDto dto)
  {
    try
    {
      Guard.IsNotNullOrWhiteSpace(dto.ViewerConnectionId);

      return (await _terminalStore.WriteInput(
        dto.TerminalId,
        dto.Input,
        dto.ViewerConnectionId,
        _appLifetime.ApplicationStopping)).ToHubResult();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating terminal session.");
      return HubResult.Fail("An error occurred.");
    }
  }

  public async Task RefreshDeviceInfo()
  {
    await _heartbeatTimer.SendDeviceHeartbeat();
  }

  public async Task<HubResult> RequestDesktopPreview(DesktopPreviewRequestDto dto)
  {
    try
    {
      _logger.LogInformation(
        "Desktop preview requested. Requester ID: {RequesterId}, Stream ID: {StreamId}, Target Process ID: {TargetProcessId}",
        dto.RequesterId,
        dto.StreamId,
        dto.TargetProcessId);

      if (!_ipcServerStore.TryGetServer(dto.TargetProcessId, out var ipcServer))
      {
        _logger.LogWarning(
          "No IPC server found for process ID {ProcessId}. Cannot request desktop preview.",
          dto.TargetProcessId);
        return HubResult.Fail("IPC server not found for target process.");
      }

      var ipcDto = new DesktopPreviewRequestIpcDto(dto.RequesterId, dto.StreamId, dto.TargetProcessId);
      var response = await ipcServer.Server.Client.GetDesktopPreview(ipcDto);

      if (!response.IsSuccess || response.JpegData.Length == 0)
      {
        _logger.LogWarning(
          "Desktop preview failed on target process {ProcessId}. Error: {Error}",
          dto.TargetProcessId,
          response.ErrorMessage ?? "Unknown error");
        return HubResult.Fail(response.ErrorMessage ?? "Desktop preview failed on target process.");
      }

      _logger.LogDebug(
        "Streaming desktop preview data. JPEG size: {Size} bytes, Stream ID: {StreamId}",
        response.JpegData.Length,
        dto.StreamId);

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
      await _hubConnection.StreamBytes(
        nameof(IAgentHub.SendDesktopPreviewStream),
        [dto.StreamId],
        response.JpegData,
        10,
        cts.Token);

      _logger.LogInformation(
        "Desktop preview stream sent successfully. Stream ID: {StreamId}",
        dto.StreamId);

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while requesting desktop preview.");
      return HubResult.Fail("An error occurred while requesting desktop preview.");
    }
  }

  public async Task<HubResult> SendChatMessage(ChatMessageHubDto dto)
  {
    try
    {
      _logger.LogDebug(
        "Chat message received. Session ID: {SessionId}, " +
        "Target System Session: {TargetSystemSession}, " +
        "Process ID: {TargetProcessId}",
        dto.SessionId,
        dto.TargetSystemSession,
        dto.TargetProcessId);

      if (!_ipcServerStore.TryGetServer(dto.TargetProcessId, out var ipcServer))
      {
        _logger.LogWarning(
          "No IPC server found for process ID {ProcessId}. Cannot send chat message.",
          dto.TargetProcessId);
        return HubResult.Fail("IPC server not found for target process.");
      }

      var ipcDto = new ChatMessageIpcDto(
        dto.SessionId,
        dto.Message,
        dto.SenderName,
        dto.SenderEmail,
        dto.TargetSystemSession,
        dto.TargetProcessId,
        dto.ViewerConnectionId,
        dto.Timestamp);

      await ipcServer.Server.Client.ReceiveChatMessage(ipcDto);
      _logger.LogInformation(
        "Chat message sent to IPC server for process ID {ProcessId}.",
        dto.TargetProcessId);

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending chat message to IPC server.");
      return HubResult.Fail("An error occurred while sending chat message.");
    }
  }

  public async Task<HubResult> StreamDirectoryContents(DirectoryContentsStreamRequestHubDto dto)
  {
    try
    {
      _logger.LogDebug(
        "Streaming directory contents: {DirectoryPath}", 
        dto.DirectoryPath);

      var result = await _fileManager.GetDirectoryContents(dto.DirectoryPath);

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
      await _hubConnection.StreamData(
        nameof(IAgentHub.SendDirectoryContentsStream),
        [dto.StreamId, result.DirectoryExists],
        result.Entries,
        AppConstants.DefaultHubDtoChunkSize,
        100,
        cts.Token);

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error streaming directory contents for {DirectoryPath}", dto.DirectoryPath);
      return HubResult.Fail("An error occurred while streaming directory contents.");
    }
  }

  public async Task<HubResult> StreamFileContents(StreamFileContentsRequestHubDto dto)
  {
    try
    {
      _logger.LogDebug(
        "Streaming file contents: {FilePath}, Stream ID: {StreamId}",
        dto.FilePath, 
        dto.StreamId);

      if (!_fileSystem.FileExists(dto.FilePath))
      {
        _logger.LogWarning("File not found: {FilePath}", dto.FilePath);
        return HubResult.Fail("File not found.");
      }

      Task
        .Run(() => SendFileStream(dto.StreamId, dto.FilePath, isTempFile: false))
        .Forget();

      _logger.LogDebug("Successfully started file stream: {FilePath}", dto.FilePath);
      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while streaming file: {FilePath}", dto.FilePath);
      return HubResult.Fail("An error occurred while streaming file.");
    }
  }

  public async Task<HubResult> StreamSubdirectories(SubdirectoriesStreamRequestHubDto dto)
  {
    try
    {
      _logger.LogDebug("Streaming subdirectories: {DirectoryPath}", dto.DirectoryPath);

      var subdirs = await _fileManager.GetSubdirectories(dto.DirectoryPath);

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
      await _hubConnection.StreamData(
        nameof(IAgentHub.SendSubdirectoriesStream),
        [dto.StreamId],
        subdirs,
        AppConstants.DefaultHubDtoChunkSize,
        100,
        cts.Token);

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error streaming subdirectories for {DirectoryPath}", dto.DirectoryPath);
      return HubResult.Fail("An error occurred while streaming subdirectories.");
    }
  }

  public async Task<HubResult> TestVncConnection(int port)
  {
    try
    {
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      var result = await _localProxy.TestConnection(port, cts.Token);
      return result.ToHubResult();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while testing VNC connection on port {Port}", port);
      return HubResult.Fail("An error occurred while testing VNC connection.");
    }
  }

  public Task UninstallAgent(string reason)
  {
    try
    {
      _logger.LogInformation("Uninstall command received.  Reason: {reason}", reason);
      var psi = new ProcessStartInfo
      {
        FileName = _systemEnvironment.StartupExePath,
        Arguments = $"uninstall -i {_settings.InstanceId}",
        UseShellExecute = true
      };
      _ = _processManager.Start(psi);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while uninstalling agent.");
    }

    return Task.CompletedTask;
  }

  public async Task<HubResult<FileDownloadResponseHubDto>> UploadFileToViewer(FileDownloadHubDto dto)
  {
    try
    {
      _logger.LogInformation("Sending file to viewer: {FilePath}, Stream ID: {StreamId}",
        dto.FilePath, dto.StreamId);

      var resolveResult = await _fileManager.ResolveTargetFilePath(dto.FilePath);
      if (!resolveResult.IsSuccess || string.IsNullOrEmpty(resolveResult.FileSystemPath))
      {
        _logger.LogWarning("Failed to prepare file for download: {FilePath}, Error: {Error}", dto.FilePath,
          resolveResult.ErrorMessage);
        return HubResult.Fail<FileDownloadResponseHubDto>(resolveResult.ErrorMessage ?? "Failed to prepare file for download");
      }

      var fileInfo = _fileSystem.GetFileInfo(resolveResult.FileSystemPath);

      Task
        .Run(() => SendFileStream(dto.StreamId, resolveResult.FileSystemPath, resolveResult.IsTempFile))
        .Forget();

      _logger.LogInformation("Successfully sent file download stream: {FilePath}", dto.FilePath);
      return HubResult.Ok(new FileDownloadResponseHubDto(fileInfo.Length, resolveResult.FileDisplayName));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending file download: {FilePath}", dto.FilePath);
      return HubResult.Fail<FileDownloadResponseHubDto>("An error occurred while sending file download.");
    }
  }

  public async Task<ValidateFilePathResponseDto> ValidateFilePath(ValidateFilePathHubDto dto)
  {
    try
    {
      _logger.LogInformation("Validating file path: {FileName} in {DirectoryPath}", dto.FileName, dto.DirectoryPath);

      var result = await _fileManager.ValidateFilePath(dto.DirectoryPath, dto.FileName);

      _logger.LogInformation("File path validation result: {IsValid}, Error: {ErrorMessage}",
        result.IsValid, result.ErrorMessage);

      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while validating file path: {FileName} in {DirectoryPath}", dto.FileName,
        dto.DirectoryPath);
      return new ValidateFilePathResponseDto(false, "An error occurred while validating file path.");
    }
  }

  private async Task SendFileStream(Guid streamId, string fileSystemPath, bool isTempFile)
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
    await using var fileStream = _fileSystem.OpenFileStream(fileSystemPath, FileMode.Open,
      FileAccess.Read, FileShare.ReadWrite);

    var channel = Channel.CreateBounded<byte[]>(10);

    var writeTask = Task.Run(async () =>
    {
      try
      {
        var buffer = new byte[AppConstants.SignalrMaxMessageSize];
        int bytesRead;
        while ((bytesRead = await fileStream.ReadAsync(buffer, cts.Token)) > 0)
        {
          await channel.Writer.WriteAsync(buffer[..bytesRead], cts.Token);
        }
        channel.Writer.TryComplete();
      }
      catch (Exception ex)
      {
        channel.Writer.TryComplete(ex);
        _logger.LogError(ex, "Error writing file stream chunks for stream ID: {StreamId}", streamId);
      }
    }, cts.Token);

    try
    {
      await _hubConnection.Send(
        nameof(IAgentHub.SendFileContentStream),
        [streamId, channel.Reader],
        cts.Token);
      await writeTask;
      _logger.LogInformation("File stream sent successfully for stream ID: {StreamId}", streamId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending file stream for stream ID: {StreamId}", streamId);
      await cts.CancelAsync();
    }
    
    if (isTempFile)
    {
      try
      {
        _logger.LogInformation("Deleting temporary file: {FilePath}", fileSystemPath);
        _fileSystem.DeleteFile(fileSystemPath);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deleting temporary file: {FilePath}", fileSystemPath);
      }
    }
  }
}
