using System.Diagnostics;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Services.Terminal;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

internal class AgentHubClient(
  IHubConnection<IAgentHub> hubConnection,
  ISystemEnvironment systemEnvironment,
  IMessenger messenger,
  ITerminalStore terminalStore,
  IUiSessionProvider osSessionProvider,
  IIpcServerStore ipcServerStore,
  IDesktopClientUpdater streamerUpdater,
  IHostApplicationLifetime appLifetime,
  ISettingsProvider settings,
  IProcessManager processManager,
  ILocalSocketProxy localProxy,
  IFileManager fileManager,
  ILogger<AgentHubClient> logger) : IAgentHubClient
{
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly IIpcServerStore _ipcServerStore = ipcServerStore;
  private readonly ILocalSocketProxy _localProxy = localProxy;
  private readonly ILogger<AgentHubClient> _logger = logger;
  private readonly IMessenger _messenger = messenger;
  private readonly IUiSessionProvider _osSessionProvider = osSessionProvider;
  private readonly IProcessManager _processManager = processManager;
  private readonly ISettingsProvider _settings = settings;
  private readonly IDesktopClientUpdater _streamerUpdater = streamerUpdater;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly IHubConnection<IAgentHub> _hubConnection = hubConnection;
  private readonly ITerminalStore _terminalStore = terminalStore;
  private readonly IFileManager _fileManager = fileManager;

  public async Task<Result> CloseChatSession(Guid sessionId, int targetProcessId)
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
        return Result.Fail("IPC server not found for target process.");
      }

      var ipcDto = new CloseChatSessionIpcDto(sessionId, targetProcessId);
      await ipcServer.Server.Send(ipcDto);

      _logger.LogInformation(
        "Chat session close request sent to IPC server for process ID {ProcessId}.",
        targetProcessId);

      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while closing chat session {SessionId}.", sessionId);
      return Result.Fail("An error occurred while closing chat session.");
    }
  }

  public async Task<bool> CreateStreamingSession(RemoteControlSessionRequestDto dto)
  {
    try
    {
      if (!_settings.DisableAutoUpdate)
      {
        var versionResult = await _streamerUpdater.EnsureLatestVersion(dto, _appLifetime.ApplicationStopping);
        if (!versionResult)
        {
          return false;
        }
      }

      _logger.LogInformation(
        "Creating streaming session.  Session ID: {SessionId}, Viewer Connection ID: {ViewerConnectionId}, " +
        "Target System Session: {TargetSystemSession}, Process ID: {TargetProcessId}",
        dto.SessionId,
        dto.ViewerConnectionId,
        dto.TargetSystemSession,
        dto.TargetProcessId);

      if (!_ipcServerStore.TryGetServer(dto.TargetProcessId, out var ipcServer))
      {
        _logger.LogWarning(
          "No IPC server found for process ID {ProcessId}.  Cannot create streaming session.",
          dto.TargetProcessId);
        return false;
      }

      var dataFolder = string.IsNullOrWhiteSpace(_settings.InstanceId)
        ? "Default"
        : _settings.InstanceId;

      var ipcDto = new RemoteControlRequestIpcDto(
        dto.SessionId,
        dto.WebsocketUri,
        dto.TargetSystemSession,
        dto.TargetProcessId,
        dto.ViewerConnectionId,
        dto.DeviceId,
        dto.NotifyUserOnSessionStart,
        dataFolder,
        dto.ViewerName);

      await ipcServer.Server.Send(ipcDto);
      _logger.LogInformation(
        "Streaming session created successfully for process ID {ProcessId}.",
        dto.TargetProcessId);

      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating streaming session.");
      return false;
    }
  }

  public async Task<Result> CreateTerminalSession(TerminalSessionRequest requestDto)
  {
    try
    {
      _logger.LogInformation("Terminal session started.  Viewer Connection ID: {ConnectionId}",
        requestDto.ViewerConnectionId);

      return await _terminalStore.CreateSession(requestDto.TerminalId, requestDto.ViewerConnectionId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating terminal session.");
      return Result.Fail("An error occurred.");
    }
  }

  public async Task<Result> CreateVncSession(VncSessionRequestDto sessionRequestDto)
  {
    try
    {
      _logger.LogInformation(
        "VNC session requested.  Viewer Connection ID: {ConnectionId}.",
        sessionRequestDto.ViewerConnectionId);

      return await _localProxy.HandleVncSession(sessionRequestDto).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating VNC session.");
      return Result.Fail("An error occurred while creating VNC session.");
    }
  }

  public async Task<DeviceUiSession[]> GetActiveUiSessions()
  {
    return await _osSessionProvider.GetActiveDesktopClients();
  }

  public async Task<Result<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto request)
  {
    try
    {
      return await _terminalStore.GetPwshCompletions(request);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting PowerShell completions.");
      return Result.Fail<PwshCompletionsResponseDto>("An error occurred while getting PowerShell completions.");
    }
  }

  public Task ReceiveDto(DtoWrapper dto)
  {
    _messenger.Send(new DtoReceivedMessage<DtoWrapper>(dto)).Forget();
    return Task.CompletedTask;
  }

  public async Task<Result> ReceiveTerminalInput(TerminalInputDto dto)
  {
    try
    {
      return await _terminalStore.WriteInput(
        dto.TerminalId,
        dto.Input,
        dto.ViewerConnectionId,
        _appLifetime.ApplicationStopping);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating terminal session.");
      return Result.Fail("An error occurred.");
    }
  }

  public async Task<Result> RequestDesktopPreview(DesktopPreviewRequestDto dto)
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
        return Result.Fail("IPC server not found for target process.");
      }

      var ipcDto = new DesktopPreviewRequestIpcDto(dto.RequesterId, dto.StreamId, dto.TargetProcessId);
      var ipcResult = await ipcServer.Server.Invoke<DesktopPreviewRequestIpcDto, DesktopPreviewResponseIpcDto>(ipcDto, 10_000);

      if (!ipcResult.IsSuccess)
      {
        _logger.LogWarning(
          "Failed to get desktop preview from process ID {ProcessId}. Error: {Error}",
          dto.TargetProcessId,
          ipcResult.Reason);
        return Result.Fail($"Failed to get desktop preview: {ipcResult.Reason}");
      }

      var response = ipcResult.Value;
      if (!response.IsSuccess || response.JpegData.Length == 0)
      {
        _logger.LogWarning(
          "Desktop preview failed on target process {ProcessId}. Error: {Error}",
          dto.TargetProcessId,
          response.ErrorMessage ?? "Unknown error");
        return Result.Fail(response.ErrorMessage ?? "Desktop preview failed on target process.");
      }
      
      // Stream the JPEG data back through SignalR
      _logger.LogInformation(
        "Streaming desktop preview data. JPEG size: {Size} bytes, Stream ID: {StreamId}",
        response.JpegData.Length,
        dto.StreamId);

      var chunkedStream = CreateChunkedStream(response.JpegData);
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
      await _hubConnection.Send(
        nameof(IAgentHub.SendDesktopPreviewStream),
        [dto.StreamId, chunkedStream],
        cts.Token
      );

      _logger.LogInformation(
        "Desktop preview stream sent successfully. Stream ID: {StreamId}",
        dto.StreamId);

      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while requesting desktop preview.");
      return Result.Fail("An error occurred while requesting desktop preview.");
    }
  }

  public async Task<Result> SendChatMessage(ChatMessageHubDto dto)
  {
    try
    {
      _logger.LogInformation(
        "Chat message received for device {DeviceId}. Session ID: {SessionId}, " +
        "Target System Session: {TargetSystemSession}, Process ID: {TargetProcessId}",
        dto.DeviceId,
        dto.SessionId,
        dto.TargetSystemSession,
        dto.TargetProcessId);

      if (!_ipcServerStore.TryGetServer(dto.TargetProcessId, out var ipcServer))
      {
        _logger.LogWarning(
          "No IPC server found for process ID {ProcessId}. Cannot send chat message.",
          dto.TargetProcessId);
        return Result.Fail("IPC server not found for target process.");
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

      await ipcServer.Server.Send(ipcDto);
      _logger.LogInformation(
        "Chat message sent to IPC server for process ID {ProcessId}.",
        dto.TargetProcessId);

      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending chat message to IPC server.");
      return Result.Fail("An error occurred while sending chat message.");
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

  public async Task<Result<GetRootDrivesResponseDto>> GetRootDrives(GetRootDrivesRequestDto requestDto)
  {
    try
    {
      _logger.LogInformation("Getting root drives for device {DeviceId}", requestDto.DeviceId);
      
      var drives = await _fileManager.GetRootDrives();
      
      return Result.Ok(new GetRootDrivesResponseDto(drives));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting root drives");
      return Result.Fail<GetRootDrivesResponseDto>("An error occurred while getting root drives.");
    }
  }

  public async Task<Result<GetSubdirectoriesResponseDto>> GetSubdirectories(GetSubdirectoriesRequestDto requestDto)
  {
    try
    {
      _logger.LogInformation("Getting subdirectories for {DeviceId}: {DirectoryPath}", 
        requestDto.DeviceId, requestDto.DirectoryPath);
      
      var subdirectories = await _fileManager.GetSubdirectories(requestDto.DirectoryPath);
      
      return Result.Ok(new GetSubdirectoriesResponseDto(subdirectories));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting subdirectories for {DirectoryPath}", requestDto.DirectoryPath);
      return Result.Fail<GetSubdirectoriesResponseDto>("An error occurred while getting subdirectories.");
    }
  }

  public async Task<Result<GetDirectoryContentsResponseDto>> GetDirectoryContents(GetDirectoryContentsRequestDto requestDto)
  {
    try
    {
      _logger.LogInformation("Getting directory contents for {DeviceId}: {DirectoryPath}", 
        requestDto.DeviceId, requestDto.DirectoryPath);
      
      var result = await _fileManager.GetDirectoryContents(requestDto.DirectoryPath);
      
      return Result.Ok(new GetDirectoryContentsResponseDto(result.Entries, result.DirectoryExists));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting directory contents for {DirectoryPath}", requestDto.DirectoryPath);
      return Result.Fail<GetDirectoryContentsResponseDto>("An error occurred while getting directory contents.");
    }
  }

  public async Task<Result?> ReceiveFileUpload(FileUploadHubDto dto)
  {
      _logger.LogInformation("Downloading file from viewer: {FileName} to {Directory}", 
        dto.FileName, dto.TargetDirectoryPath);

      var stream = _hubConnection.Server.GetFileUploadStream(dto);
      var targetPath = Path.Join(dto.TargetDirectoryPath, dto.FileName);
      using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
      await foreach (var chunk in stream)
      {
        // Process each chunk (e.g., write to file, buffer, etc.)
        await fs.WriteAsync(chunk);
      }
      return Result.Ok();
  }

  public async Task<Result> SendFileDownload(FileDownloadHubDto dto)
  {
    try
    {
      _logger.LogInformation("Sending file download: {FilePath}, Stream ID: {StreamId}, Is Directory: {IsDirectory}", 
        dto.FilePath, dto.StreamId, dto.IsDirectory);

      var prepareResult = await _fileManager.PrepareFileForDownload(dto.FilePath, dto.IsDirectory);
      if (!prepareResult.IsSuccess || string.IsNullOrEmpty(prepareResult.TempFilePath))
      {
        _logger.LogWarning("Failed to prepare file for download: {FilePath}, Error: {Error}", dto.FilePath, prepareResult.ErrorMessage);
        return Result.Fail(prepareResult.ErrorMessage ?? "Failed to prepare file for download");
      }

      try
      {
        // Read the file and create a chunked stream
        var fileBytes = await File.ReadAllBytesAsync(prepareResult.TempFilePath);
        var chunkStream = CreateChunkedStream(fileBytes);

        // Send the stream to the hub
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        await _hubConnection.Send(
          nameof(IAgentHub.SendFileDownloadStream),
          [dto.StreamId, chunkStream],
          cts.Token
        );

        _logger.LogInformation("Successfully sent file download stream: {FilePath}", dto.FilePath);
        return Result.Ok();
      }
      finally
      {
        // Clean up temporary file if it was created for a directory
        if (dto.IsDirectory && File.Exists(prepareResult.TempFilePath))
        {
          try
          {
            File.Delete(prepareResult.TempFilePath);
            _logger.LogDebug("Deleted temporary ZIP file: {TempFilePath}", prepareResult.TempFilePath);
          }
          catch (Exception cleanupEx)
          {
            _logger.LogWarning(cleanupEx, "Failed to delete temporary ZIP file: {TempFilePath}", prepareResult.TempFilePath);
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending file download: {FilePath}", dto.FilePath);
      return Result.Fail("An error occurred while sending file download.");
    }
  }

  public async Task<Result> DeleteFile(FileDeleteHubDto dto)
  {
    try
    {
      _logger.LogInformation("Deleting {ItemType}: {FilePath}", dto.IsDirectory ? "directory" : "file", dto.FilePath);

      var result = await _fileManager.DeleteFile(dto.FilePath, dto.IsDirectory);
      
      if (result.IsSuccess)
      {
        _logger.LogInformation("Successfully deleted {ItemType}: {FilePath}", dto.IsDirectory ? "directory" : "file", dto.FilePath);
        return Result.Ok();
      }
      else
      {
        _logger.LogWarning("Failed to delete {ItemType}: {FilePath}, Error: {Error}", 
          dto.IsDirectory ? "directory" : "file", dto.FilePath, result.ErrorMessage);
        return Result.Fail(result.ErrorMessage ?? "Failed to delete file");
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while deleting {ItemType}: {FilePath}", dto.IsDirectory ? "directory" : "file", dto.FilePath);
      return Result.Fail("An error occurred while deleting file.");
    }
  }

  private static async IAsyncEnumerable<byte[]> CreateChunkedStream(byte[] data)
  {
    const int chunkSize = 30 * 1024; // 30KB chunks

    foreach (var chunk in data.Chunk(chunkSize))
    {
      yield return chunk;
      await Task.Delay(1); // Small delay to prevent overwhelming the connection
    }
  }
}