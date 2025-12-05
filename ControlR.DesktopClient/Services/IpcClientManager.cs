using Avalonia.Controls.ApplicationLifetimes;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Dtos.HubDtos;
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
  IControlledApplicationLifetime appLifetime,
  IDesktopPreviewProvider desktopPreviewService,
  IServiceProvider serviceProvider,
  IOptions<DesktopClientOptions> desktopClientOptions,
  ILogger<IpcClientManager> logger) : BackgroundService
{
  private readonly IControlledApplicationLifetime _appLifetime = appLifetime;
  private readonly IChatSessionManager _chatSessionManager = chatSessionManager;
  private readonly IOptions<DesktopClientOptions> _desktopClientOptions = desktopClientOptions;
  private readonly IDesktopPreviewProvider _desktopPreviewService = desktopPreviewService;
  private readonly IIpcClientAccessor _ipcClientAccessor = ipcClientAccessor;
  private readonly IIpcConnectionFactory _ipcConnectionFactory = ipcConnectionFactory;
  private readonly ILogger<IpcClientManager> _logger = logger;
  private readonly IRemoteControlHostManager _remoteControlHostManager = remoteControlHostManager;
  private readonly IServiceProvider _serviceProvider = serviceProvider;
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
        client.On<CheckOsPermissionsIpcDto, CheckOsPermissionsResponseIpcDto>(HandleCheckOsPermissions);
        client.On<ShutdownCommandDto>(HandleShutdownCommand);
        client.On<InvokeCtrlAltDelRequestDto>(HandleInvokeCtrlAltDel);

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

            _appLifetime.Shutdown(1);
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
  private CheckOsPermissionsResponseIpcDto HandleCheckOsPermissions(CheckOsPermissionsIpcDto dto)
  {
    try
    {
      _logger.LogInformation("Handling OS permissions check request for process ID: {ProcessId}", dto.TargetProcessId);

      var arePermissionsGranted = false;

#if MAC_BUILD
      var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
      var isAccessibilityGranted = macInterop.IsAccessibilityPermissionGranted();
      var isScreenCaptureGranted = macInterop.IsScreenCapturePermissionGranted();
      arePermissionsGranted = isAccessibilityGranted && isScreenCaptureGranted;

      _logger.LogInformation(
        "macOS permissions check: Accessibility={Accessibility}, ScreenCapture={ScreenCapture}",
        isAccessibilityGranted,
        isScreenCaptureGranted);
#elif LINUX_BUILD
      var detector = _serviceProvider.GetRequiredService<IDesktopEnvironmentDetector>();
      if (detector.IsWayland())
      {
        var waylandPermissions = _serviceProvider.GetRequiredService<IWaylandPermissionProvider>();
        arePermissionsGranted = waylandPermissions.IsRemoteControlPermissionGranted().GetAwaiter().GetResult();

        _logger.LogInformation("Wayland permissions check: RemoteControl={RemoteControl}", arePermissionsGranted);
      }
      else
      {
        // X11 doesn't require special permissions
        arePermissionsGranted = true;
        _logger.LogInformation("X11 detected, no special permissions required");
      }
#else
      // Windows doesn't require special OS-level permissions for remote control
      arePermissionsGranted = true;
      _logger.LogInformation("Windows detected, no special permissions required");
#endif

      return new CheckOsPermissionsResponseIpcDto(arePermissionsGranted);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while checking OS permissions.");
      return new CheckOsPermissionsResponseIpcDto(false);
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
  private void HandleInvokeCtrlAltDel(InvokeCtrlAltDelRequestDto dto)
  {
    if (!OperatingSystem.IsWindows())
    {
      _logger.LogWarning("Ctrl+Alt+Del invocation requested on non-Windows OS. Ignoring.");
      return;
    }

#if WINDOWS_BUILD
     _logger.LogInformation("Handling Ctrl+Alt+Del request. Requester ID: {RequesterId}", dto.InvokerUserName);
     var win32Interop = _serviceProvider.GetRequiredService<IWin32Interop>();
     win32Interop.InvokeCtrlAltDel();
#endif
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
      _appLifetime.Shutdown(0);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling shutdown command.");
    }
  }
}