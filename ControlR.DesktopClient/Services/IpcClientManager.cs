using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Services;

public class IpcClientManager(
  TimeProvider timeProvider,
  IRemoteControlHostManager remoteControlHostManager,
  IChatSessionManager chatSessionManager,
  IIpcConnectionFactory ipcConnectionFactory,
  IProcessManager processManager,
  ILogger<IpcClientManager> logger) : BackgroundService, IIpcResponseSender
{
  private readonly IIpcConnectionFactory _ipcConnectionFactory = ipcConnectionFactory;
  private readonly IRemoteControlHostManager _remoteControlHostManager = remoteControlHostManager;
  private readonly IChatSessionManager _chatSessionManager = chatSessionManager;
  private readonly ILogger<IpcClientManager> _logger = logger;
  private readonly IProcessManager _processManager = processManager;
  private readonly TimeProvider _timeProvider = timeProvider;
  private IIpcClient? _currentConnection;

  public async Task<bool> SendChatResponse(ChatResponseIpcDto response)
  {
    try
    {
      if (_currentConnection is null)
      {
        _logger.LogWarning("No active IPC connection available to send chat response");
        return false;
      }

      await _currentConnection.Send(response);
      _logger.LogInformation(
        "Chat response sent for session {SessionId} from {Username}",
        response.SessionId,
        response.SenderUsername);
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending chat response via IPC");
      return false;
    }
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    await AcceptClientConnections(stoppingToken);
  }

  private async Task AcceptClientConnections(CancellationToken stoppingToken)
  {
    var processId = _processManager.GetCurrentProcess().Id;
    var pipeName = IpcPipeNames.GetPipeName();

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        _logger.LogInformation("Attempting to connect to IPC server. Pipe Name: {PipeName}", pipeName);

        using var client = await _ipcConnectionFactory.CreateClient(".", pipeName);
        client.On<RemoteControlRequestIpcDto>(HandleRemoteControlRequest);
        client.On<ChatMessageIpcDto>(HandleChatMessage);

        if (!await client.Connect(stoppingToken))
        {
          _logger.LogWarning("Failed to connect to IPC server.");
          await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, stoppingToken);
          continue;
        }

        _logger.LogInformation("Connected to IPC server.");
        _currentConnection = client;
        client.BeginRead(stoppingToken);
        _logger.LogInformation("Read started.");

        _logger.LogInformation("Sending client identity attestation. Process ID: {ProcessId}", processId);
        var dto = new IpcClientIdentityAttestationDto(processId);
        await client.Send(dto, stoppingToken);

        _logger.LogInformation("Waiting for connection end.");
        await client.WaitForConnectionEnd(stoppingToken);
        _currentConnection = null;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while connecting to IPC server.");
      }

      await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, stoppingToken);
    }
  }

  private void HandleRemoteControlRequest(RemoteControlRequestIpcDto dto)
  {
     _remoteControlHostManager.StartHost(dto).Forget();
  }

  private async void HandleChatMessage(ChatMessageIpcDto dto)
  {
    try
    {
      _logger.LogInformation(
        "Handling chat message. Session ID: {SessionId}, Sender: {SenderName} ({SenderEmail})",
        dto.SessionId,
        dto.SenderName,
        dto.SenderEmail);

      // Create or get the chat session
      if (!_chatSessionManager.IsSessionActive(dto.SessionId))
      {
        await _chatSessionManager.CreateChatSession(
          dto.SessionId,
          dto.TargetSystemSession,
          dto.TargetProcessId,
          dto.ViewerConnectionId);
      }

      // Add the message to the session
      await _chatSessionManager.AddMessage(dto.SessionId, dto);

      // TODO: Show chat UI notification or update existing chat window
      _logger.LogInformation(
        "Chat message received from {SenderName}: {Message}",
        dto.SenderName,
        dto.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling chat message.");
    }
  }
}