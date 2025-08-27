using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace ControlR.DesktopClient.Services;

public class IpcClientManager(
  TimeProvider timeProvider,
  IRemoteControlHostManager remoteControlHostManager,
  IChatSessionManager chatSessionManager,
  IpcClientAccessor ipcClientAccessor,
  IIpcConnectionFactory ipcConnectionFactory,
  IProcessManager processManager,
  IScreenGrabber screenGrabber,
  IImageUtility imageUtility,
  ILogger<IpcClientManager> logger) : BackgroundService
{
  private readonly IChatSessionManager _chatSessionManager = chatSessionManager;
  private readonly IpcClientAccessor _ipcClientAccessor = ipcClientAccessor;
  private readonly IIpcConnectionFactory _ipcConnectionFactory = ipcConnectionFactory;
  private readonly ILogger<IpcClientManager> _logger = logger;
  private readonly IProcessManager _processManager = processManager;
  private readonly IRemoteControlHostManager _remoteControlHostManager = remoteControlHostManager;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IScreenGrabber _screenGrabber = screenGrabber;
  private readonly IImageUtility _imageUtility = imageUtility;

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
        client.On<DesktopPreviewRequestIpcDto, DesktopPreviewResponseIpcDto>(HandleDesktopPreviewRequest);

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
      using var captureResult = _screenGrabber.Capture(captureCursor: false);
      if (!captureResult.IsSuccess || captureResult.Bitmap is null)
      {
        _logger.LogWarning("Failed to capture displays: {Error}", captureResult.FailureReason);
        return new DesktopPreviewResponseIpcDto([], false, captureResult.FailureReason);
      }

      // Encode as JPEG
      var jpegData = _imageUtility.EncodeJpeg(captureResult.Bitmap, 75, compressOutput: false); // 75% quality
      if (jpegData is null || jpegData.Length == 0)
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
}