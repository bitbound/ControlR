using Avalonia.Controls.ApplicationLifetimes;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Services;

public class IpcClientManager(
  TimeProvider timeProvider,
  IRemoteControlHostManager remoteControlHostManager,
  IChatSessionManager chatSessionManager,
  IIpcClientAccessor ipcClientAccessor,
  IIpcConnectionFactory ipcConnectionFactory,
  IClassicDesktopStyleApplicationLifetime appLifetime,
  IDesktopPreviewProvider desktopPreviewService,
  IOptions<DesktopClientOptions> desktopClientOptions,
  ILogger<IpcClientManager> logger) : BackgroundService
{
  private readonly IClassicDesktopStyleApplicationLifetime _appLifetime = appLifetime;
  private readonly IChatSessionManager _chatSessionManager = chatSessionManager;
  private readonly IOptions<DesktopClientOptions> _desktopClientOptions = desktopClientOptions;
  private readonly IDesktopPreviewProvider _desktopPreviewService = desktopPreviewService;
  private readonly IIpcClientAccessor _ipcClientAccessor = ipcClientAccessor;
  private readonly IIpcConnectionFactory _ipcConnectionFactory = ipcConnectionFactory;
  private readonly ILogger<IpcClientManager> _logger = logger;
  private readonly IRemoteControlHostManager _remoteControlHostManager = remoteControlHostManager;
  private readonly TimeProvider _timeProvider = timeProvider;
  private DateTimeOffset? _firstConnectionAttempt;


  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    await CreateClientConnection(stoppingToken);
  }

  private async Task CreateClientConnection(CancellationToken stoppingToken)
  {
    var pipeName = IpcPipeNames.GetPipeName(_desktopClientOptions.Value.InstanceId);
    var connectionTimeout = TimeSpan.FromSeconds(60);

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        _logger.LogInformation("Attempting to connect to IPC server. Pipe Name: {PipeName}", pipeName);

        using var client = await _ipcConnectionFactory.CreateClient(".", pipeName);
        client.On<RemoteControlRequestIpcDto>(HandleRemoteControlRequest);
        client.On<ChatMessageIpcDto>(HandleChatMessage);
        client.On<CloseChatSessionIpcDto>(HandleCloseChatSession);
        client.On<DesktopPreviewRequestIpcDto, DesktopPreviewResponseIpcDto>(HandleDesktopPreviewRequest);
        client.On<ShutdownCommandDto>(HandleShutdownCommand);

        if (!await client.Connect(stoppingToken))
        {
          _logger.LogWarning("Failed to connect to IPC server.");

          // Track the first connection attempt
          _firstConnectionAttempt ??= _timeProvider.GetUtcNow();

          // Check if we've exceeded the connection timeout
          var elapsed = _timeProvider.GetUtcNow() - _firstConnectionAttempt.Value;
          if (elapsed > connectionTimeout)
          {
            _logger.LogError(
              "Unable to connect to IPC server after {Elapsed:N0} seconds. Shutting down.",
              elapsed.TotalSeconds);

            if (!_appLifetime.TryShutdown())
            {
              _logger.LogWarning("Failed to initiate application shutdown.");
            }
            return;
          }

          await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, stoppingToken);
          continue;
        }

        _logger.LogInformation("Connected to IPC server.");

        // Reset the connection attempt tracker on successful connection
        _firstConnectionAttempt = null;

        _ipcClientAccessor.SetConnection(client);
        client.BeginRead(stoppingToken);
        _logger.LogInformation("Read started. Waiting for connection end.");
        await client.WaitForConnectionEnd(stoppingToken);
        _ipcClientAccessor.SetConnection(null);
      }
      catch (OperationCanceledException ex)
      {
        _logger.LogInformation(ex, "App shutting down. Stopping IpcClientManager.");
        break;
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

  private DesktopPreviewResponseIpcDto HandleDesktopPreviewRequest(DesktopPreviewRequestIpcDto dto)
  {
    try
    {
      _logger.LogInformation(
        "Handling desktop preview request. Requester ID: {RequesterId}, Stream ID: {StreamId}, Process ID: {ProcessId}",
        dto.RequesterId,
        dto.StreamId,
        dto.TargetProcessId);

      // Capture preview (synchronous wait for async task)
      var result = _desktopPreviewService.CapturePreview().GetAwaiter().GetResult();

      if (!result.IsSuccess)
      {
        _logger.LogWarning("Failed to capture preview: {Error}", result.Reason);
        return new DesktopPreviewResponseIpcDto([], false, result.Reason);
      }

      _logger.LogInformation(
        "Desktop preview captured successfully. JPEG size: {Size} bytes",
        result.Value.Length);

      return new DesktopPreviewResponseIpcDto(result.Value, true);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling desktop preview request.");
      return new DesktopPreviewResponseIpcDto([], false, "An error occurred while capturing desktop preview.");
    }
  }

  private void HandleRemoteControlRequest(RemoteControlRequestIpcDto dto)
  {
    _remoteControlHostManager.StartHost(dto).Forget();
  }

  private async void HandleShutdownCommand(ShutdownCommandDto dto)
  {
    try
    {
      _logger.LogInformation("Handling shutdown command. Reason: {Reason}", dto.Reason);
      await _remoteControlHostManager.StopAllHosts(dto.Reason);
      if (!_appLifetime.TryShutdown())
      {
        _logger.LogWarning("Failed to initiate application shutdown.");
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling shutdown command.");
    }
  }
}