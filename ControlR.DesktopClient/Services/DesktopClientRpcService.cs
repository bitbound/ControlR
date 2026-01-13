using Avalonia.Controls.ApplicationLifetimes;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.Ipc.Interfaces;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Services;

public class DesktopClientRpcService(
    IServiceProvider serviceProvider,
    IChatSessionManager chatSessionManager,
    IDesktopPreviewProvider desktopPreviewService,
    IRemoteControlHostManager remoteControlHostManager,
    IControlledApplicationLifetime appLifetime,
    ILogger<DesktopClientRpcService> logger) : IDesktopClientRpcService
{
    private readonly IControlledApplicationLifetime _appLifetime = appLifetime;
    private readonly IChatSessionManager _chatSessionManager = chatSessionManager;
    private readonly IDesktopPreviewProvider _desktopPreviewService = desktopPreviewService;
    private readonly ILogger<DesktopClientRpcService> _logger = logger;
    private readonly IRemoteControlHostManager _remoteControlHostManager = remoteControlHostManager;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public async Task<CheckOsPermissionsResponseIpcDto> CheckOsPermissions(CheckOsPermissionsIpcDto dto)
    {
        try
        {
            _logger.LogInformation("Handling OS permissions check request for process ID: {ProcessId}", dto.TargetProcessId);

            var arePermissionsGranted = false;

            if (OperatingSystem.IsMacOS())
            {
                var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
                var isAccessibilityGranted = macInterop.IsMacAccessibilityPermissionGranted();
                var isScreenCaptureGranted = macInterop.IsMacScreenCapturePermissionGranted();
                arePermissionsGranted = isAccessibilityGranted && isScreenCaptureGranted;

                _logger.LogInformation(
                  "macOS permissions check: Accessibility={Accessibility}, ScreenCapture={ScreenCapture}",
                  isAccessibilityGranted,
                  isScreenCaptureGranted);
            }
            else if (OperatingSystem.IsLinux())
            {
                var detector = _serviceProvider.GetRequiredService<IDesktopEnvironmentDetector>();
                if (detector.IsWayland())
                {
                    var waylandPermissions = _serviceProvider.GetRequiredService<IWaylandPermissionProvider>();
                    arePermissionsGranted = await waylandPermissions.IsRemoteControlPermissionGranted();

                    _logger.LogInformation("Wayland permissions check: RemoteControl={RemoteControl}", arePermissionsGranted);
                }
                else
                {
                    // X11 doesn't require special permissions
                    arePermissionsGranted = true;
                    _logger.LogInformation("X11 detected, no special permissions required");
                }
            }
            else
            {
                // Windows doesn't require special OS-level permissions for remote control
                arePermissionsGranted = true;
                _logger.LogInformation("Windows detected, no special permissions required");
            }

            return new CheckOsPermissionsResponseIpcDto(arePermissionsGranted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking OS permissions.");
            return new CheckOsPermissionsResponseIpcDto(false);
        }
    }
    public async Task CloseChatSession(CloseChatSessionIpcDto dto)
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
    public async Task<DesktopPreviewResponseIpcDto> GetDesktopPreview(DesktopPreviewRequestIpcDto dto)
    {
        try
        {
            _logger.LogInformation(
              "Handling desktop preview request. Requester ID: {RequesterId}, Stream ID: {StreamId}, Process ID: {ProcessId}",
              dto.RequesterId,
              dto.StreamId,
              dto.TargetProcessId);

            // Capture preview (synchronous wait for async task)
            var result = await _desktopPreviewService.CapturePreview();

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
    public Task InvokeCtrlAltDel(InvokeCtrlAltDelRequestDto dto)
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("Ctrl+Alt+Del invocation requested on non-Windows OS. Ignoring.");
            return Task.CompletedTask;
        }


        _logger.LogInformation("Handling Ctrl+Alt+Del request. Requester ID: {RequesterId}", dto.InvokerUserName);
        var win32Interop = _serviceProvider.GetRequiredService<IWin32Interop>();
        win32Interop.InvokeCtrlAltDel();
        return Task.CompletedTask;
    }
    public async Task ReceiveChatMessage(ChatMessageIpcDto dto)
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
    public Task<Result> ReceiveRemoteControlRequest(RemoteControlRequestIpcDto dto)
    {
        return _remoteControlHostManager.StartHost(dto);
    }
    public async Task ShutdownDesktopClient(ShutdownCommandDto dto)
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
