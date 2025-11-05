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
  IScreenGrabber screenGrabber,
  IImageUtility imageUtility,
  IOptions<DesktopClientOptions> desktopClientOptions,
  ILogger<IpcClientManager> logger) : BackgroundService
{
  private readonly IClassicDesktopStyleApplicationLifetime _appLifetime = appLifetime;
  private readonly IChatSessionManager _chatSessionManager = chatSessionManager;
  private readonly IOptions<DesktopClientOptions> _desktopClientOptions = desktopClientOptions;
  private readonly IImageUtility _imageUtility = imageUtility;
  private readonly IIpcClientAccessor _ipcClientAccessor = ipcClientAccessor;
  private readonly IIpcConnectionFactory _ipcConnectionFactory = ipcConnectionFactory;
  private readonly ILogger<IpcClientManager> _logger = logger;
  private readonly IRemoteControlHostManager _remoteControlHostManager = remoteControlHostManager;
  private readonly IScreenGrabber _screenGrabber = screenGrabber;
  private readonly TimeProvider _timeProvider = timeProvider;


  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    await CreateClientConnection(stoppingToken);
  }

  private async Task CreateClientConnection(CancellationToken stoppingToken)
  {
    var pipeName = IpcPipeNames.GetPipeName(_desktopClientOptions.Value.InstanceId);

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
          await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, stoppingToken);
          continue;
        }

        _logger.LogInformation("Connected to IPC server.");
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

      // Capture all displays (synchronous call)
      using var captureResult = _screenGrabber.CaptureAllDisplays(captureCursor: false);
      if (!captureResult.IsSuccess || captureResult.Bitmap is null)
      {
        _logger.LogWarning("Failed to capture displays: {Error}", captureResult.FailureReason);
        return new DesktopPreviewResponseIpcDto([], false, captureResult.FailureReason);
      }

      // Encode as JPEG
      var jpegData = _imageUtility.EncodeJpeg(captureResult.Bitmap, 75, compressOutput: false);
      if (jpegData.Length == 0)
      {
        _logger.LogWarning("Failed to encode JPEG: No data returned");
        return new DesktopPreviewResponseIpcDto([], false, "Failed to encode JPEG");
      }

      _logger.LogInformation(
        "Desktop preview captured successfully. JPEG size: {Size} bytes",
        jpegData.Length);

      return new DesktopPreviewResponseIpcDto(jpegData, true);
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