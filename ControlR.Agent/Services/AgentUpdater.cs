﻿using ControlR.Agent.Options;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Http;
using ControlR.Libraries.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ControlR.Agent.Services;

internal interface IAgentUpdater : IHostedService
{
    Task CheckForUpdate(CancellationToken cancellationToken = default);
    ManualResetEventAsync UpdateCheckCompletedSignal { get; }
}

internal class AgentUpdater(
    IVersionApi _versionApi,
    IDownloadsApi _downloadsApi,
    IReleasesApi _releasesApi,
    IFileSystem _fileSystem,
    IProcessManager _processInvoker,
    IEnvironmentHelper _environmentHelper,
    ISettingsProvider _settings,
    IHostApplicationLifetime _appLifetime,
    IOptions<InstanceOptions> _instanceOptions,
    ILogger<AgentUpdater> logger) : BackgroundService, IAgentUpdater
{
    private readonly SemaphoreSlim _checkForUpdatesLock = new(1, 1);
    private readonly ILogger<AgentUpdater> _logger = logger;

    public ManualResetEventAsync UpdateCheckCompletedSignal { get; } = new ManualResetEventAsync(false);

    public async Task CheckForUpdate(CancellationToken cancellationToken = default)
    {
        if (_environmentHelper.IsDebug)
        {
            return;
        }

        using var logScope = _logger.BeginMemberScope();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _appLifetime.ApplicationStopping);

        if (!await _checkForUpdatesLock.WaitAsync(0, linkedCts.Token))
        {
            _logger.LogWarning("Failed to acquire lock in agent updater.  Aborting check.");
            return;
        }

        try
        {
            UpdateCheckCompletedSignal.Reset();

            _logger.LogInformation("Beginning version check.");


            var hashResult = await _versionApi.GetCurrentAgentHash(_environmentHelper.Runtime);
            if (!hashResult.IsSuccess)
            {
                return;
            }
            var remoteHash = hashResult.Value;
            var serverOrigin = _settings.ServerUri.ToString().TrimEnd('/');
            var downloadPath = AppConstants.GetAgentFileDownloadPath(_environmentHelper.Runtime);
            var downloadUrl = $"{serverOrigin}{downloadPath}";

            using var startupExeFs = _fileSystem.OpenFileStream(_environmentHelper.StartupExePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var startupExeHash = await SHA256.HashDataAsync(startupExeFs, linkedCts.Token);

            _logger.LogInformation(
                "Comparing local file hash {LocalFileHash} to latest file hash {ServerFileHash}",
                Convert.ToHexString(startupExeHash),
                Convert.ToHexString(remoteHash));

            if (remoteHash.SequenceEqual(startupExeHash))
            {
                _logger.LogInformation("Version is current.");
                return;
            }

            _logger.LogInformation("Update found. Downloading update.");

            var tempDirPath = string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId) ?
                Path.Combine(Path.GetTempPath(), "ControlR_Update") :
                Path.Combine(Path.GetTempPath(), "ControlR_Update", _instanceOptions.Value.InstanceId);

            _ = _fileSystem.CreateDirectory(tempDirPath);
            var tempPath = Path.Combine(tempDirPath, AppConstants.GetAgentFileName(_environmentHelper.Platform));

            if (_fileSystem.FileExists(tempPath))
            {
                _fileSystem.DeleteFile(tempPath);
            }

            var result = await _downloadsApi.DownloadFile(downloadUrl, tempPath);
            if (!result.IsSuccess)
            {
                _logger.LogCritical("Download failed.  Aborting update.");
                return;
            }

            using (var tempFs = _fileSystem.OpenFileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var updateHash = await SHA256.HashDataAsync(tempFs, linkedCts.Token);
                var updateHexHash = Convert.ToHexString(updateHash);

                if (_settings.IsConnectedToPublicServer &&
                    !await _releasesApi.DoesReleaseHashExist(updateHexHash))
                {
                    _logger.LogCritical("A new agent version is available, but the hash does not exist in the public releases data.");
                    return;
                }
            }

            _logger.LogInformation("Launching installer.");

            var installCommand = "install";
            if (_instanceOptions.Value.InstanceId is string instanceId)
            {
                installCommand += $" -i {instanceId}";
            }

            switch (_environmentHelper.Platform)
            {
                case SystemPlatform.Windows:
                    {
                        await _processInvoker
                            .Start(tempPath, installCommand)
                            .WaitForExitAsync(linkedCts.Token);
                    }
                    break;

                case SystemPlatform.Linux:
                    {
                        await _processInvoker
                          .Start("sudo", $"chmod +x {tempPath}")
                          .WaitForExitAsync(linkedCts.Token);

                        await _processInvoker.StartAndWaitForExit(
                            "/bin/bash", 
                            $"-c \"{tempPath} {installCommand} &\"", 
                            true,
                            linkedCts.Token);
                    }
                    break;

                case SystemPlatform.MacOS:
                    {
                        await _processInvoker
                         .Start("sudo", $"chmod +x {tempPath}")
                         .WaitForExitAsync(linkedCts.Token);

                        await _processInvoker.StartAndWaitForExit(
                            "/bin/zsh",
                            $"-c \"{tempPath} {installCommand} &\"",
                            true,
                            linkedCts.Token);
                    }
                    break;

                default:
                    throw new PlatformNotSupportedException();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for updates.");
        }
        finally
        {
            UpdateCheckCompletedSignal.Set();
            _checkForUpdatesLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_environmentHelper.IsDebug)
        {
            return;
        }

        await CheckForUpdate(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckForUpdate(stoppingToken);
        }
    }
}