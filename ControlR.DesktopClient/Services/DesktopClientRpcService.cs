using Avalonia.Controls.ApplicationLifetimes;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.Ipc.Interfaces;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Services;

public class DesktopClientRpcService(
    IChatSessionManager chatSessionManager,
    IDesktopClientPermissionService desktopClientPermissionService,
    IDesktopPreviewProvider desktopPreviewService,
    IPlatformIpcMessageHandler platformMessageHandler,
    IRemoteControlHostManager remoteControlHostManager,
    IControlledApplicationLifetime appLifetime,
    ILogger<DesktopClientRpcService> logger) : IDesktopClientRpcService
{
  private readonly IControlledApplicationLifetime _appLifetime = appLifetime;
  private readonly IChatSessionManager _chatSessionManager = chatSessionManager;
  private readonly IDesktopClientPermissionService _desktopClientPermissionService = desktopClientPermissionService;
  private readonly IDesktopPreviewProvider _desktopPreviewService = desktopPreviewService;
  private readonly ILogger<DesktopClientRpcService> _logger = logger;
  private readonly IPlatformIpcMessageHandler _platformMessageHandler = platformMessageHandler;
  private readonly IRemoteControlHostManager _remoteControlHostManager = remoteControlHostManager;

  public async Task<CheckOsPermissionsResponseIpcDto> CheckOsPermissions(CheckOsPermissionsIpcDto dto)
  {
    try
    {
      _logger.LogInformation(
        "Handling OS permissions check request for {Scope}. Process ID: {ProcessId}",
        dto.Scope,
        dto.TargetProcessId);

      var permissionState = await _desktopClientPermissionService.GetPermissionState(dto.Scope);
      var response = new CheckOsPermissionsResponseIpcDto(
          permissionState.ArePermissionsGranted,
          permissionState.Reason);

      _logger.LogInformation(
        "Desktop client permission check result for {Scope}: Granted={Granted}, Reason={Reason}",
        dto.Scope,
        response.ArePermissionsGranted,
        response.Reason ?? "None");

      return response;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while checking OS permissions for {Scope}.", dto.Scope);
      return new CheckOsPermissionsResponseIpcDto(false, "Unable to determine desktop client permissions.");
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

      var permissionState = await CheckOsPermissions(
          new CheckOsPermissionsIpcDto(
              dto.TargetProcessId,
              DesktopClientPermissionScope.DesktopPreview));

      if (!permissionState.ArePermissionsGranted)
      {
        _logger.LogWarning(
            "Desktop preview denied for process ID {ProcessId}. Reason: {Reason}",
            dto.TargetProcessId,
            permissionState.Reason ?? "Unknown reason");
        return new DesktopPreviewResponseIpcDto([], false, permissionState.Reason ?? "Desktop preview permission is not granted.");
      }

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

  public Task<DesktopSessionInfoResponseIpcDto> GetDesktopSessionInfo()
  {
    return _platformMessageHandler.GetDesktopSessionInfo();
  }

  public Task InvokeCtrlAltDel(InvokeCtrlAltDelRequestDto dto)
  {
    return _platformMessageHandler.InvokeCtrlAltDel(dto);
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

      await _chatSessionManager.AddMessage(dto.SessionId, dto);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling chat message.");
    }
  }

  public async Task<Result> ReceiveRemoteControlRequest(RemoteControlRequestIpcDto dto)
  {
    var permissionState = await CheckOsPermissions(
        new CheckOsPermissionsIpcDto(
            dto.TargetProcessId,
            DesktopClientPermissionScope.RemoteControl));

    if (!permissionState.ArePermissionsGranted)
    {
      _logger.LogWarning(
          "Remote control denied for process ID {ProcessId}. Reason: {Reason}",
          dto.TargetProcessId,
          permissionState.Reason ?? "Unknown reason");
      return Result.Fail(permissionState.Reason ?? "Remote control permission is not granted.");
    }

    return (await _remoteControlHostManager.StartHost(dto)).ToResult();
  }

  public async Task<CheckOsPermissionsResponseIpcDto> RequestRemoteControlPermission(RequestRemoteControlPermissionIpcDto dto)
  {
    try
    {
      _logger.LogInformation(
        "Handling remote control permission request for {Scope}. Process ID: {ProcessId}",
        dto.Scope,
        dto.TargetProcessId);

      var permissionState = await _desktopClientPermissionService.RequestPermission(dto.Scope);

      _logger.LogInformation(
        "Remote control permission request result for {Scope}: Granted={Granted}, Reason={Reason}",
        dto.Scope,
        permissionState.ArePermissionsGranted,
        permissionState.Reason ?? "None");

      return new CheckOsPermissionsResponseIpcDto(
          permissionState.ArePermissionsGranted,
          permissionState.Reason);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while requesting remote control permissions for {Scope}.", dto.Scope);
      return new CheckOsPermissionsResponseIpcDto(false, "Unable to request desktop client permissions.");
    }
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
