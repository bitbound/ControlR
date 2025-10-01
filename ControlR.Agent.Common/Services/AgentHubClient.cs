using System.Diagnostics;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Services.FileManager;
using ControlR.Agent.Common.Services.Terminal;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

// ReSharper disable once ClassNeverInstantiated.GLobal
internal class AgentHubClient(
  IHubConnection<IAgentHub> hubConnection,
  ISystemEnvironment systemEnvironment,
  IMessenger messenger,
  ITerminalStore terminalStore,
  IUiSessionProvider uiSessionProvider,
  IIpcServerStore ipcServerStore,
  IDesktopClientUpdater streamerUpdater,
  IHostApplicationLifetime appLifetime,
  ISettingsProvider settings,
  IProcessManager processManager,
  ILocalSocketProxy localProxy,
  IFileManager fileManager,
  IFileSystem fileSystem,
  ILogger<AgentHubClient> logger) : IAgentHubClient
{
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly IDesktopClientUpdater _desktopClientUpdater = streamerUpdater;
  private readonly IFileManager _fileManager = fileManager;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IHubConnection<IAgentHub> _hubConnection = hubConnection;
  private readonly IIpcServerStore _ipcServerStore = ipcServerStore;
  private readonly ILocalSocketProxy _localProxy = localProxy;
  private readonly ILogger<AgentHubClient> _logger = logger;
  private readonly IMessenger _messenger = messenger;
  private readonly IProcessManager _processManager = processManager;
  private readonly ISettingsProvider _settings = settings;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly ITerminalStore _terminalStore = terminalStore;
  private readonly IUiSessionProvider _uiSessionProvider = uiSessionProvider;

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

  public async Task<Result> CreateDirectory(CreateDirectoryHubDto dto)
  {
    try
    {
      _logger.LogInformation("Creating directory: {DirectoryName} in {ParentPath}", dto.DirectoryName, dto.ParentPath);

      var result = await _fileManager.CreateDirectory(dto.ParentPath, dto.DirectoryName);

      if (result.IsSuccess)
      {
        _logger.LogInformation("Successfully created directory: {DirectoryName} in {ParentPath}", dto.DirectoryName,
          dto.ParentPath);
        return Result.Ok();
      }

      _logger.LogWarning("Failed to create directory: {DirectoryName} in {ParentPath}, Error: {Error}",
        dto.DirectoryName, dto.ParentPath, result.ErrorMessage);
      return Result.Fail(result.ErrorMessage ?? "Failed to create directory");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating directory: {DirectoryName} in {ParentPath}", dto.DirectoryName,
        dto.ParentPath);
      return Result.Fail("An error occurred while creating directory.");
    }
  }

  public async Task<Result> CreateRemoteControlSession(RemoteControlSessionRequestDto dto)
  {
    try
    {
      if (!_settings.DisableAutoUpdate)
      {
        var versionResult = await _desktopClientUpdater.EnsureLatestVersion(dto, _appLifetime.ApplicationStopping);
        if (!versionResult)
        {
          return Result.Fail("Failed to ensure latest desktop client version is installed.");
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
        return Result.Fail($"Process with ID {dto.TargetProcessId} is no longer running.");
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

      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating streaming session.");
      return Result.Fail("An error occurred on the agent while creating streaming session.");
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

  public async Task<Result> DeleteFile(FileDeleteHubDto dto)
  {
    try
    {
      _logger.LogInformation("Delete file system entry: {FilePath}", dto.TargetPath);

      var result = await _fileManager.DeleteFileSystemEntry(dto.TargetPath);

      if (result.IsSuccess)
      {
        _logger.LogInformation("Successfully deleted file system entry: {FilePath}", dto.TargetPath);
        return Result.Ok();
      }

      _logger.LogWarning("Failed to delete file system entry: {FilePath}, Error: {Error}",
        dto.TargetPath, result.ErrorMessage);
      return Result.Fail(result.ErrorMessage ?? "Failed to delete file system entry");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while deleting file system entry: {FilePath}", dto.TargetPath);
      return Result.Fail("An error occurred while deleting file system entry.");
    }
  }

  public async Task<Result?> DownloadFileFromViewer(FileUploadHubDto dto)
  {
    _logger.LogInformation("Downloading file from viewer: {FileName} to {Directory}",
      dto.FileName, dto.TargetDirectoryPath);

    var targetPath = Path.Join(dto.TargetDirectoryPath, dto.FileName);

    // Check if file already exists and overwrite is not allowed
    if (_fileSystem.FileExists(targetPath) && !dto.Overwrite)
    {
      _logger.LogWarning("File already exists and overwrite is not allowed: {FilePath}", targetPath);
      return Result.Fail("File already exists");
    }

    var stream = _hubConnection.Server.GetFileUploadStream(dto);
    await using var fs = _fileSystem.OpenFileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
    await foreach (var chunk in stream)
    {
      // Process each chunk (e.g., write to file, buffer, etc.)
      await fs.WriteAsync(chunk);
    }

    return Result.Ok();
  }

  public async Task<DeviceUiSession[]> GetActiveUiSessions()
  {
    return await _uiSessionProvider.GetActiveDesktopClients();
  }

  public async Task<PathSegmentsResponseDto> GetPathSegments(GetPathSegmentsHubDto dto)
  {
    try
    {
      _logger.LogInformation("Getting path segments for: {TargetPath}", dto.TargetPath);

      var result = await _fileManager.GetPathSegments(dto.TargetPath);

      _logger.LogInformation(
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
      var ipcResult =
        await ipcServer.Server.Invoke<DesktopPreviewRequestIpcDto, DesktopPreviewResponseIpcDto>(ipcDto, 10_000);

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

  public async Task<Result> StreamDirectoryContents(DirectoryContentsStreamRequestHubDto dto)
  {
    try
    {
      _logger.LogInformation("Streaming directory contents for {DeviceId}: {DirectoryPath}", dto.DeviceId,
        dto.DirectoryPath);

      var result = await _fileManager.GetDirectoryContents(dto.DirectoryPath);
      var chunkSize = _settings.HubDtoChunkSize;
      var chunks = CreateChunks(result.Entries, chunkSize); // configurable chunk size

      await _hubConnection.Server.SendDirectoryContentsStream(
        dto.StreamId,
        result.DirectoryExists,
        chunks);

      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error streaming directory contents for {DirectoryPath}", dto.DirectoryPath);
      return Result.Fail("An error occurred while streaming directory contents.");
    }
  }

  // Removed legacy non-streaming GetSubdirectories/GetDirectoryContents hub methods in favor of streaming variants.
  public async Task<Result> StreamSubdirectories(SubdirectoriesStreamRequestHubDto dto)
  {
    try
    {
      _logger.LogInformation("Streaming subdirectories for {DeviceId}: {DirectoryPath}", dto.DeviceId,
        dto.DirectoryPath);
      var subdirs = await _fileManager.GetSubdirectories(dto.DirectoryPath);
      var chunkSize = _settings.HubDtoChunkSize;
      var chunks = CreateChunks(subdirs, chunkSize);

      await _hubConnection.Server.SendSubdirectoriesStream(dto.StreamId, chunks);
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error streaming subdirectories for {DirectoryPath}", dto.DirectoryPath);
      return Result.Fail("An error occurred while streaming subdirectories.");
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

  public async Task<Result<FileDownloadResponseHubDto>> UploadFileToViewer(FileDownloadHubDto dto)
  {
    try
    {
      _logger.LogInformation("Sending file to viewer: {FilePath}, Stream ID: {StreamId}",
        dto.FilePath, dto.StreamId);

      using var resolveResult = await _fileManager.ResolveTargetFilePath(dto.FilePath);
      if (!resolveResult.IsSuccess || string.IsNullOrEmpty(resolveResult.FileSystemPath))
      {
        _logger.LogWarning("Failed to prepare file for download: {FilePath}, Error: {Error}", dto.FilePath,
          resolveResult.ErrorMessage);
        return Result.Fail<FileDownloadResponseHubDto>(resolveResult.ErrorMessage ??
                                                       "Failed to prepare file for download");
      }

      // Read the file and create a chunked stream
      await using var fileStream = _fileSystem.OpenFileStream(resolveResult.FileSystemPath, FileMode.Open,
        FileAccess.Read, FileShare.ReadWrite);
      using var ms = new MemoryStream();
      await fileStream.CopyToAsync(ms);
      var chunkStream = CreateChunkedStream(ms.ToArray());

      // Send the stream to the hub
      using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
      await _hubConnection.Send(
        nameof(IAgentHub.SendFileDownloadStream),
        [dto.StreamId, chunkStream],
        cts.Token
      );

      _logger.LogInformation("Successfully sent file download stream: {FilePath}", dto.FilePath);
      return Result.Ok(new FileDownloadResponseHubDto(ms.Length, resolveResult.FileDisplayName));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending file download: {FilePath}", dto.FilePath);
      return Result.Fail<FileDownloadResponseHubDto>("An error occurred while sending file download.");
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

  private static async IAsyncEnumerable<byte[]> CreateChunkedStream(byte[] data)
  {
    const int chunkSize = 30 * 1024; // 30KB chunks

    foreach (var chunk in data.Chunk(chunkSize))
    {
      yield return chunk;
      await Task.Delay(1);
    }
  }

  private static async IAsyncEnumerable<T[]> CreateChunks<T>(IEnumerable<T> source, int chunkSize)
  {
    if (chunkSize <= 0)
    {
      chunkSize = 400;
    }

    var buffer = new List<T>(chunkSize);
    foreach (var item in source)
    {
      buffer.Add(item);
      if (buffer.Count < chunkSize)
      {
        continue;
      }

      yield return buffer.ToArray();
      buffer.Clear();
      await Task.Yield();
    }

    if (buffer.Count > 0)
    {
      yield return buffer.ToArray();
    }
  }
}