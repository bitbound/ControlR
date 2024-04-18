using ControlR.Agent.Interfaces;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Collections;
using ControlR.Shared.Dtos;
using ControlR.Shared.Extensions;
using ControlR.Shared.Primitives;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Security.Cryptography;

namespace ControlR.Agent.Services.Windows;

internal class StreamerUpdaterWindows(
    IServiceProvider _serviceProvider,
    IFileSystem _fileSystem,
    IDownloadsApi _downloadsApi,
    IEnvironmentHelper _environmentHelper,
    IVersionApi _versionApi,
    ISettingsProvider _settings,
    ILogger<StreamerUpdaterWindows> _logger) : BackgroundService, IStreamerUpdater
{
    private readonly ConcurrentList<StreamerSessionRequestDto> _pendingRequests = [];
    private readonly string _remoteControlZipUri = $"{_settings.ServerUri}downloads/{AppConstants.RemoteControlZipFileName}";
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private double _previousProgress = 0;

    public async Task<Result> EnsureLatestVersion(StreamerSessionRequestDto requestDto, CancellationToken cancellationToken)
    {
        _pendingRequests.Add(requestDto);
        try
        {
            return await EnsureLatestVersion(cancellationToken);
        }
        finally
        {
            _pendingRequests.Remove(requestDto);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_environmentHelper.IsDebug)
        {
            return;
        }

        await EnsureLatestVersion(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await EnsureLatestVersion(stoppingToken);
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

    private async Task<Result> EnsureLatestVersion(CancellationToken cancellationToken)
    {
        await _updateLock.WaitAsync(cancellationToken);
        try
        {

            var startupDir = _environmentHelper.StartupDirectory;
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
                _previousProgress = 0;
                var downloadResult = await DownloadRemoteControl(remoteControlDir, ReportDownloadProgress);
                if (!downloadResult.IsSuccess)
                {
                    return downloadResult;
                }
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            var result = Result.Fail(ex, "Error while ensuring remote control latest version.");
            _logger.LogResult(result);
            return result;            
        }
        finally
        {
            _updateLock.Release();
        }
    }
    private async Task ReportDownloadProgress(double progress)
    {
        var connection = _serviceProvider.GetRequiredService<IAgentHubConnection>();

        if (progress == 1 || progress - _previousProgress > .05)
        {
            _previousProgress = progress;

            foreach (var request in _pendingRequests)
            {
                try
                {
                    await connection
                        .SendRemoteControlDownloadProgress(
                            request.StreamingSessionId, 
                            request.ViewerConnectionId, 
                            progress)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while sending remote control download progress.");
                }
            }
        }
    }
}
