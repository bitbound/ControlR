using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
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
  IpcClientAccessor ipcClientAccessor,
  IIpcConnectionFactory ipcConnectionFactory,
  IProcessManager processManager,
  ILogger<IpcClientManager> logger) : BackgroundService
{
  private readonly IChatSessionManager _chatSessionManager = chatSessionManager;
  private readonly IpcClientAccessor _ipcClientAccessor = ipcClientAccessor;
  private readonly IIpcConnectionFactory _ipcConnectionFactory = ipcConnectionFactory;
  private readonly ILogger<IpcClientManager> _logger = logger;
  private readonly IProcessManager _processManager = processManager;
  private readonly IRemoteControlHostManager _remoteControlHostManager = remoteControlHostManager;
  private readonly TimeProvider _timeProvider = timeProvider;

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
        client.On<CloseChatSessionIpcDto>(HandleCloseChatSession);

        if (!await client.Connect(stoppingToken))
        {
          _logger.LogWarning("Failed to connect to IPC server.");
          await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, stoppingToken);
          continue;
        }

        _logger.LogInformation("Connected to IPC server.");
        _ipcClientAccessor.SetConnection(client);
        client.BeginRead(stoppingToken);
        _logger.LogInformation("Read started.");

        _logger.LogInformation("Sending client identity attestation. Process ID: {ProcessId}", processId);
        var dto = new IpcClientIdentityAttestationDto(processId);
        await client.Send(dto, stoppingToken);

        _logger.LogInformation("Waiting for connection end.");
        await client.WaitForConnectionEnd(stoppingToken);
        _ipcClientAccessor.SetConnection(null);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while connecting to IPC server.");
      }

      try
      {
        await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, stoppingToken);
      }
      catch (OperationCanceledException)
      {
        _logger.LogInformation("App shutting down. Stopping IpcClientManager.");
        break;
      }
    }
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

      // Add the message to the session
      await _chatSessionManager.AddMessage(dto.SessionId, dto);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling chat message.");
    }
  }

  private async void HandleCloseChatSession(CloseChatSessionIpcDto dto)
  {
    try
    {
      _logger.LogInformation(
        "Handling close chat session request. Session ID: {SessionId}, Process ID: {ProcessId}",
        dto.SessionId,
        dto.TargetProcessId);

      // Close the session through the chat session manager
      await _chatSessionManager.CloseChatSession(dto.SessionId, true);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling close chat session request.");
    }
  }

  private void HandleRemoteControlRequest(RemoteControlRequestIpcDto dto)
  {
    _remoteControlHostManager.StartHost(dto).Forget();
  }
}