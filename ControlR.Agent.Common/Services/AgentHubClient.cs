using System.Diagnostics;
using System.Runtime.Versioning;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Services.Terminal;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
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

      //await _hubConnection.Server.SendDesktopPreviewStream(dto.StreamId, chunkedStream);

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