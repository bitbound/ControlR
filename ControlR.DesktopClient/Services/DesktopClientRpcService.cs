using System.IO;
using System.Diagnostics;
using Avalonia.Controls.ApplicationLifetimes;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Ipc.Interfaces;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Services;

public class DesktopClientRpcService(
    IServiceProvider serviceProvider,
    IChatSessionManager chatSessionManager,
    IDesktopClientPermissionService desktopClientPermissionService,
    IDesktopPreviewProvider desktopPreviewService,
    IRemoteControlHostManager remoteControlHostManager,
    IControlledApplicationLifetime appLifetime,
    IIpcClientAccessor ipcClientAccessor,
    ILogger<DesktopClientRpcService> logger) : IDesktopClientRpcService
{
  private readonly IControlledApplicationLifetime _appLifetime = appLifetime;
  private readonly IChatSessionManager _chatSessionManager = chatSessionManager;
  private readonly IDesktopClientPermissionService _desktopClientPermissionService = desktopClientPermissionService;
  private readonly IDesktopPreviewProvider _desktopPreviewService = desktopPreviewService;
  private readonly ILogger<DesktopClientRpcService> _logger = logger;
  private readonly IRemoteControlHostManager _remoteControlHostManager = remoteControlHostManager;
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  private readonly IIpcClientAccessor _ipcClientAccessor = ipcClientAccessor;

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

  public Task InvokeCtrlAltDel(InvokeCtrlAltDelRequestDto dto)
  {
#if IS_WINDOWS
        _logger.LogInformation("Handling Ctrl+Alt+Del request. Requester ID: {RequesterId}", dto.InvokerUserName);
        var win32Interop = _serviceProvider.GetRequiredService<IWin32Interop>();
        win32Interop.InvokeCtrlAltDel();
#else
    _logger.LogWarning("Ctrl+Alt+Del invocation requested on non-Windows OS. Ignoring.");
#endif
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

  public async Task ExecuteScript(ExecuteScriptIpcDto dto)
  {
    Task.Run(async () =>
    {
      string? tempFilePath = null;
      string? stdoutPath = null;
      string? stderrPath = null;
      try
      {
        var ext = dto.ShellType switch
        {
          ShellType.PowerShell => ".ps1",
          ShellType.Cmd => ".bat",
          ShellType.Bash => ".sh",
          _ => ".txt"
        };
        tempFilePath = Path.Combine(Path.GetTempPath(), $"controlr_script_{dto.ExecutionId}{ext}");
        await File.WriteAllTextAsync(tempFilePath, dto.ScriptContent);

        var runElevated = dto.RunAs == ScriptRunAs.CurrentUserElevated;
        var isWindows = OperatingSystem.IsWindows();

        var isAlreadyElevated = false;
        if (isWindows)
        {
#pragma warning disable CA1416
          using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
          var principal = new System.Security.Principal.WindowsPrincipal(identity);
          isAlreadyElevated = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
#pragma warning restore CA1416
        }

        var needUacElevation = runElevated && isWindows && !isAlreadyElevated;

        string fileName;
        string arguments;

        if (needUacElevation)
        {
          stdoutPath = Path.Combine(Path.GetTempPath(), $"controlr_script_{dto.ExecutionId}_out.txt");
          stderrPath = Path.Combine(Path.GetTempPath(), $"controlr_script_{dto.ExecutionId}_err.txt");

          if (dto.ShellType == ShellType.PowerShell)
          {
            fileName = "powershell.exe";
            arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"& {{ & '{tempFilePath}' }} > '{stdoutPath}' 2> '{stderrPath}'\"";
          }
          else // Cmd
          {
            fileName = "cmd.exe";
            arguments = $"/c \"\"{tempFilePath}\" > \"{stdoutPath}\" 2> \"{stderrPath}\"\"";
          }
        }
        else
        {
          if (dto.ShellType == ShellType.PowerShell)
          {
            fileName = isWindows ? "powershell.exe" : "pwsh";
            arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{tempFilePath}\"";
          }
          else if (dto.ShellType == ShellType.Cmd)
          {
            fileName = "cmd.exe";
            arguments = $"/c \"{tempFilePath}\"";
          }
          else // Bash
          {
            fileName = "/bin/bash";
            arguments = $"\"{tempFilePath}\"";
          }
        }

        var startInfo = new ProcessStartInfo
        {
          FileName = fileName,
          Arguments = arguments,
          UseShellExecute = needUacElevation,
          CreateNoWindow = true
        };

        if (needUacElevation)
        {
          startInfo.Verb = "runas";
        }
        else
        {
          startInfo.RedirectStandardOutput = true;
          startInfo.RedirectStandardError = true;
        }

        using var process = new Process { StartInfo = startInfo };

        if (!needUacElevation)
        {
          process.OutputDataReceived += async (sender, e) =>
          {
            if (e.Data != null)
            {
              await SendOutputChunk(dto.ExecutionId, e.Data + Environment.NewLine, string.Empty, false, null);
            }
          };

          process.ErrorDataReceived += async (sender, e) =>
          {
            if (e.Data != null)
            {
              await SendOutputChunk(dto.ExecutionId, string.Empty, e.Data + Environment.NewLine, false, null);
            }
          };
        }

        process.Start();

        if (!needUacElevation)
        {
          process.BeginOutputReadLine();
          process.BeginErrorReadLine();
        }

        if (needUacElevation)
        {
          var stdoutFileOffset = 0L;
          var stderrFileOffset = 0L;

          while (!process.HasExited)
          {
            await Task.Delay(500);
            stdoutFileOffset = await ReadNewFileContent(dto.ExecutionId, stdoutPath, stdoutFileOffset, isError: false);
            stderrFileOffset = await ReadNewFileContent(dto.ExecutionId, stderrPath, stderrFileOffset, isError: true);
          }

          await Task.Delay(100);
          stdoutFileOffset = await ReadNewFileContent(dto.ExecutionId, stdoutPath, stdoutFileOffset, isError: false);
          stderrFileOffset = await ReadNewFileContent(dto.ExecutionId, stderrPath, stderrFileOffset, isError: true);
        }
        else
        {
          await process.WaitForExitAsync();
        }

        await SendOutputChunk(dto.ExecutionId, string.Empty, string.Empty, true, process.ExitCode);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error executing script {ExecutionId} on DesktopClient", dto.ExecutionId);
        await SendOutputChunk(dto.ExecutionId, string.Empty, $"DesktopClient Error: {ex.Message}" + Environment.NewLine, true, -1);
      }
      finally
      {
        DeleteFileSafe(tempFilePath);
        DeleteFileSafe(stdoutPath);
        DeleteFileSafe(stderrPath);
      }
    }).Forget();
  }

  private async Task<long> ReadNewFileContent(Guid executionId, string? path, long offset, bool isError)
  {
    if (string.IsNullOrEmpty(path) || !File.Exists(path))
    {
      return offset;
    }

    try
    {
      using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      if (fs.Length > offset)
      {
        fs.Seek(offset, SeekOrigin.Begin);
        using var reader = new StreamReader(fs);
        var content = await reader.ReadToEndAsync();
        if (!string.IsNullOrEmpty(content))
        {
          if (isError)
          {
            await SendOutputChunk(executionId, string.Empty, content, false, null);
          }
          else
          {
            await SendOutputChunk(executionId, content, string.Empty, false, null);
          }
        }
        return fs.Length;
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to read output file {Path}", path);
    }
    return offset;
  }

  private void DeleteFileSafe(string? path)
  {
    if (!string.IsNullOrEmpty(path) && File.Exists(path))
    {
      try { File.Delete(path); } catch { }
    }
  }

  private async Task SendOutputChunk(Guid executionId, string stdout, string stderr, bool isFinished, int? exitCode)
  {
    if (_ipcClientAccessor.TryGetClient(out var connection))
    {
      try
      {
        await connection.Server.SendScriptOutput(new ScriptOutputIpcDto(executionId, stdout, stderr, isFinished, exitCode));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to send script output chunk via IPC.");
      }
    }
  }
}
