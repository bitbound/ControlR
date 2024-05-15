using ControlR.Agent.Dtos;
using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Devices.Common.Native.Windows;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Extensions;
using ControlR.Shared.Primitives;
using ControlR.Shared.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleIpc;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Result = ControlR.Shared.Primitives.Result;

namespace ControlR.Agent.Services.Windows;

[SupportedOSPlatform("windows6.0.6000")]
internal class RemoteControlLauncherWindows(
    IHostApplicationLifetime _appLifetime,
    IWin32Interop _win32Interop,
    IProcessManager _processes,
    IIpcRouter _ipcRouter,
    IEnvironmentHelper _environment,
    IStreamingSessionCache _streamingSessionCache,
    ISettingsProvider _settings,
    IFileSystem _fileSystem,
    ILogger<RemoteControlLauncherWindows> _logger) : IRemoteControlLauncher
{
    private readonly SemaphoreSlim _createSessionLock = new(1, 1);
    public async Task<Result> CreateSession(
        Guid sessionId,
        byte[] authorizedKey,
        int targetWindowsSession = -1,
        bool notifyViewerOnSessionStart = false,
        bool lowerUacDuringSession = false,
        string? viewerName = null,
        Func<double, Task>? onDownloadProgress = null)
    {
        await _createSessionLock.WaitAsync();

        try
        {
            var echoResult = await GetCurrentInputDesktop(targetWindowsSession);

            if (!echoResult.IsSuccess)
            {
                _logger.LogResult(echoResult);
                return Result.Fail("Failed to determine initial input desktop.");
            }

            var targetDesktop = echoResult.Value.Trim();
            _logger.LogInformation("Starting streamer in desktop: {Desktop}", targetDesktop);

            var authorizedKeyBase64 = Convert.ToBase64String(authorizedKey);

            var session = new StreamingSession(sessionId, lowerUacDuringSession);

            var serverUri = _settings.ServerUri.ToString().TrimEnd('/');
            var args = $"--session-id={sessionId} --server-uri={serverUri} --authorized-key={authorizedKeyBase64} --notify-user={notifyViewerOnSessionStart}";
            if (!string.IsNullOrWhiteSpace(viewerName))
            {
                args += $" --viewer-name=\"{viewerName}\"";
            }

            _logger.LogInformation("Launching remote control with args: {StreamerArguments}", args);

            if (!_environment.IsDebug)
            {
                var startupDir = _environment.StartupDirectory;
                var remoteControlDir = Path.Combine(startupDir, "RemoteControl");
                var binaryPath = Path.Combine(remoteControlDir, AppConstants.RemoteControlFileName);

                _win32Interop.CreateInteractiveSystemProcess(
                    $"\"{binaryPath}\" {args}",
                    targetSessionId: targetWindowsSession,
                    forceConsoleSession: false,
                    desktopName: targetDesktop,
                    hiddenWindow: false,
                    out var process);

                if (process is null || process.Id == -1)
                {
                    var streamerZipPath = Path.Combine(startupDir, AppConstants.RemoteControlZipFileName);
                    // Delete streamer files so a clean install will be performed on the next attempt.
                    _fileSystem.DeleteDirectory(remoteControlDir, true);
                    _fileSystem.DeleteFile(streamerZipPath);
                    return Result.Fail("Failed to start remote control process.");
                }

                session.StreamerProcess = process;
            }
            else
            {
                if (_environment.IsDebug)
                {
                    args += " --dev";
                }

                var solutionDirReult = GetSolutionDir(Environment.CurrentDirectory);

                if (solutionDirReult.IsSuccess)
                {
                    var desktopDir = Path.Combine(solutionDirReult.Value, "ControlR.Streamer");
                    var psi = new ProcessStartInfo()
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/k npm run start -- -- {args}",
                        WorkingDirectory = desktopDir,
                        UseShellExecute = true
                    };
                    session.StreamerProcess = _processes.Start(psi);
                }

                if (session.StreamerProcess is null)
                {
                    return Result.Fail("Failed to start remote control process.");
                }
            }

            _streamingSessionCache.AddOrUpdate(session);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating remote control session.");
            return Result.Fail("An error occurred while starting remote control.");
        }
        finally
        {
            _createSessionLock.Release();
        }
    }

    // For debugging.
    private static Result<string> GetSolutionDir(string currentDir)
    {
        var dirInfo = new DirectoryInfo(currentDir);
        if (!dirInfo.Exists)
        {
            return Result.Fail<string>("Not found.");
        }

        if (dirInfo.GetFiles().Any(x => x.Name == "ControlR.sln"))
        {
            return Result.Ok(currentDir);
        }

        if (dirInfo.Parent is not null)
        {
            return GetSolutionDir(dirInfo.Parent.FullName);
        }

        return Result.Fail<string>("Not found.");
    }

    private async Task<Result<string>> GetCurrentInputDesktop(int targetWindowsSession)
    {
        var pipeName = Guid.NewGuid().ToString();
        var pipeSecurity = new PipeSecurity();
        var authedUsersId = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
        var systemId = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        pipeSecurity.AddAccessRule(new PipeAccessRule(authedUsersId, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        pipeSecurity.AddAccessRule(new PipeAccessRule(systemId, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        using var ipcServer = await _ipcRouter.CreateServer(pipeName, pipeSecurity);
        
        Process? process = null;

        if (Environment.UserInteractive)
        {
            process = _processes.Start(
                _environment.StartupExePath,
                $"echo-desktop --pipe-name {pipeName}");
        }
        else
        {
            _win32Interop.CreateInteractiveSystemProcess(
                $"\"{_environment.StartupExePath}\" echo-desktop --pipe-name {pipeName}",
                targetSessionId: targetWindowsSession,
                forceConsoleSession: false,
                desktopName: "Default",
                hiddenWindow: true,
                out process);
        }

        if (process is null || process.Id == -1)
        {
            return Result.Fail<string>("Failed to start echo-desktop process.");
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_appLifetime.ApplicationStopping, cts.Token);
        if (!await ipcServer.WaitForConnection(linkedCts.Token))
        {
            return Result.Fail<string>("Failed to connect to echo-desktop process.");
        }

        ipcServer.BeginRead(_appLifetime.ApplicationStopping);
        _logger.LogInformation("Desktop echoer connected to pipe server.");
        var desktopResult = await ipcServer.Invoke<DesktopRequestDto, DesktopResponseDto>(new());
        if (desktopResult.IsSuccess)
        {
            _logger.LogInformation("Received current input desktop from echoer: {CurrentDesktop}", desktopResult.Value.DesktopName);
            await ipcServer.Send(new ShutdownRequestDto());
            return Result.Ok(desktopResult.Value.DesktopName);
        }
        _logger.LogError("Failed to get initial desktop from desktop echo.");
        return Result.Fail<string>(desktopResult.Error);
    }
}