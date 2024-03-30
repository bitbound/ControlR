﻿using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Agent.Models.IpcDtos;
using ControlR.Devices.Common.Native.Windows;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Extensions;
using ControlR.Shared.Primitives;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleIpc;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Result = ControlR.Shared.Primitives.Result;

namespace ControlR.Agent.Services.Windows;

[SupportedOSPlatform("windows6.0.6000")]
internal class RemoteControlLauncherWindows(
    IFileSystem _fileSystem,
    IProcessManager _processes,
    IDownloadsApi _downloadsApi,
    IEnvironmentHelper _environment,
    IStreamingSessionCache _streamingSessionCache,
    IIpcRouter _ipcRouter,
    IHostApplicationLifetime _hostLifetime,
    IServiceProvider _serviceProvider,
    ISettingsProvider _settings,
    IVersionApi _versionApi,
    ILogger<RemoteControlLauncherWindows> _logger) : IRemoteControlLauncher
{
    private readonly SemaphoreSlim _createSessionLock = new(1, 1);
    private readonly string _remoteControlZipUri = $"{_settings.ServerUri}downloads/{AppConstants.RemoteControlZipFileName}";
    private readonly string _watcherBinaryPath = _environment.StartupExePath;
    public async Task<Result> CreateSession(
        Guid sessionId,
        byte[] authorizedKey,
        int targetWindowsSession = -1,
        string targetDesktop = "",
        bool notifyViewerOnSessionStart = false,
        string? viewerName = null,
        Func<double, Task>? onDownloadProgress = null)
    {
        await _createSessionLock.WaitAsync();

        try
        {
            var authorizedKeyBase64 = Convert.ToBase64String(authorizedKey);

            var session = new StreamingSession(sessionId, authorizedKey, targetWindowsSession, targetDesktop);

            var watcherResult = await LaunchNewSidecarProcess(session);

            if (!watcherResult.IsSuccess)
            {
                _logger.LogResult(watcherResult);
                return Result.Fail("Failed to start desktop watcher process.");
            }

            var serverUri = _settings.ServerUri.ToString().TrimEnd('/');
            var args = $"--session-id={sessionId} --server-uri={serverUri} --authorized-key={authorizedKeyBase64} --notify-user={notifyViewerOnSessionStart}";
            if (!string.IsNullOrWhiteSpace(viewerName))
            {
                args += $" --viewer-name=\"{viewerName}\"";
            }

            _logger.LogInformation("Launching remote control with args: {StreamerArguments}", args);

            if (_processes.GetCurrentProcess().SessionId == 0)
            {
                var startupDir = _environment.StartupDirectory;
                var remoteControlDir = Path.Combine(startupDir, "RemoteControl");
                var binaryPath = Path.Combine(remoteControlDir, AppConstants.RemoteControlFileName);
                var zipPath = Path.Combine(remoteControlDir, AppConstants.RemoteControlZipFileName);

                if (_fileSystem.FileExists(zipPath))
                {
                    var archiveCheckResult = await CheckArchiveHashWithRemote(zipPath, remoteControlDir);
                    if (!archiveCheckResult.IsSuccess)
                    {
                        return archiveCheckResult;
                    }
                }
                else if (_fileSystem.DirectoryExists(remoteControlDir))
                {
                    // If the archive doesn't exist, clear out any remaining files.
                    // Then future update checks will work normally.
                    _fileSystem.DeleteDirectory(remoteControlDir, true);
                }

                if (!_fileSystem.FileExists(binaryPath))
                {
                    var downloadResult = await DownloadRemoteControl(remoteControlDir, onDownloadProgress);
                    if (!downloadResult.IsSuccess)
                    {
                        return downloadResult;
                    }
                }

                Win32.CreateInteractiveSystemProcess(
                    $"\"{binaryPath}\" {args}",
                    targetSessionId: targetWindowsSession,
                    forceConsoleSession: false,
                    desktopName: session.LastDesktop,
                    hiddenWindow: false,
                    out var process);

                if (process is null || process.Id == -1)
                {
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

            _streamingSessionCache.Sessions.AddOrUpdate(
               sessionId,
               session,
               (k, v) => session);

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

    private async Task<Result> CheckArchiveHashWithRemote(string zipPath, string remoteControlDir)
    {
        byte[] localHash = [];

        using (var zipFs = _fileSystem.OpenFileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            localHash = await MD5.HashDataAsync(zipFs);
        }

        _logger.LogInformation("Checking streamer remote archive hash.");
        var streamerHashResult = await _versionApi.GetCurrentStreamerHash();
        if (!streamerHashResult.IsSuccess)
        {
            return streamerHashResult.ToResult();
        }

        _logger.LogInformation(
            "Comparing local streamer archive hash ({LocalArchiveHash}) to remote ({RemoteArchiveHash}).",
            Convert.ToBase64String(localHash),
            Convert.ToBase64String(streamerHashResult.Value));

        if (streamerHashResult.Value.SequenceEqual(localHash))
        {
            _logger.LogInformation("Versions match.  Continuing.");
        }
        else
        {
            _logger.LogInformation("Versions differ.  Removing outdated files.");
            _fileSystem.DeleteDirectory(remoteControlDir, true);
        }
        return Result.Ok();
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

    private async Task<Result> DownloadRemoteControl(string remoteControlDir, Func<double, Task>? onDownloadProgress)
    {
        try
        {
            _fileSystem.CreateDirectory(remoteControlDir);
            var targetPath = Path.Combine(remoteControlDir, AppConstants.RemoteControlZipFileName);
            var result = await _downloadsApi.DownloadRemoteControlZip(targetPath, _remoteControlZipUri, onDownloadProgress);
            if (!result.IsSuccess)
            {
                return result;
            }
            ZipFile.ExtractToDirectory(targetPath, remoteControlDir);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while extracting remote control archive.");
            return Result.Fail(ex);
        }
    }

    private async Task<Result> LaunchNewSidecarProcess(StreamingSession session)
    {
        if (_processes.GetCurrentProcess().SessionId == 0)
        {
            var args = $"--parent-id {Environment.ProcessId} --agent-pipe \"{session.AgentPipeName}\"";
            Win32.CreateInteractiveSystemProcess(
                $"\"{_watcherBinaryPath}\" sidecar {args}",
                targetSessionId: session.TargetWindowsSession,
                forceConsoleSession: false,
                desktopName: session.LastDesktop,
                hiddenWindow: true,
                out var process);

            if (process is null || process.Id == -1)
            {
                _logger.LogError("Failed to start streamer process watcher.");
            }
            else
            {
                session.WatcherProcess = process;
            }
        }
        else
        {
            var args = $"sidecar --parent-id {Environment.ProcessId} --agent-pipe \"{session.AgentPipeName}\"";
            var process = _processes.Start(_watcherBinaryPath, args);
            session.WatcherProcess = process;

            if (process is null)
            {
                _logger.LogError("Failed to start streamer process watcher.");
            }
        }

        if (session.WatcherProcess?.HasExited != false)
        {
            _logger.LogError("Watching process is unexpectedly null.");
            return Result.Fail("Watcher process failed to start.");
        }

        _logger.LogInformation("Creating pipe server for desktop watcher: {name}", session.AgentPipeName);
        session.IpcServer = await _ipcRouter.CreateServer(session.AgentPipeName);
        session.IpcServer.On<DesktopChangeDto>(async dto =>
        {
            var agentHub = _serviceProvider.GetRequiredService<IAgentHubConnection>();
            var desktopName = dto.DesktopName.Trim();

            if (!string.IsNullOrWhiteSpace(desktopName) &&
                !string.Equals(session.LastDesktop, desktopName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Desktop has changed from {LastDesktop} to {CurrentDesktop}.  Notifying viewer.",
                    session.LastDesktop,
                    desktopName);

                session.LastDesktop = desktopName;
                await agentHub.NotifyViewerDesktopChanged(session.SessionId, desktopName);
            }
        });

        var result = await session.IpcServer.WaitForConnection(_hostLifetime.ApplicationStopping);
        if (result)
        {
            session.IpcServer.BeginRead(_hostLifetime.ApplicationStopping);
            _logger.LogInformation("Desktop watcher connected to pipe server.");
            var desktopResult = await session.IpcServer.Invoke<DesktopRequestDto, DesktopChangeDto>(new());
            if (desktopResult.IsSuccess)
            {
                session.LastDesktop = desktopResult.Value.DesktopName;
                return Result.Ok();
            }
            _logger.LogError("Failed to get initial desktop from watcher.");
            return Result.Fail(desktopResult.Error);
        }
        else
        {
            _logger.LogWarning("Desktop watcher failed to connect to pipe server.");
            return Result.Fail("Desktop watcher failed to connect to pipe server.");
        }
    }
}