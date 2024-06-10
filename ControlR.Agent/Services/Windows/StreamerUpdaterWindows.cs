using ControlR.Agent.Interfaces;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Collections;
using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Http;
using ControlR.Libraries.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Octodiff.Core;
using Octodiff.Diagnostics;
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
    IAgentUpdater _agentUpdater,
    ILogger<StreamerUpdaterWindows> _logger) : BackgroundService, IStreamerUpdater
{
    private readonly ConcurrentList<StreamerSessionRequestDto> _pendingRequests = [];
    private readonly IProgressReporter _progressReporter = new ConsoleProgressReporter();
    private readonly string _streamerZipUri = $"{_settings.ServerUri}downloads/{AppConstants.StreamerZipFileName}";
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private double _previousProgress = 0;

    public async Task<bool> EnsureLatestVersion(StreamerSessionRequestDto requestDto, CancellationToken cancellationToken)
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

    public async Task<bool> EnsureLatestVersion(CancellationToken cancellationToken)
    {
        await _agentUpdater.UpdateCheckCompletedSignal.Wait(cancellationToken);
        await _updateLock.WaitAsync(cancellationToken);
        try
        {

            var startupDir = _environmentHelper.StartupDirectory;
            var streamerDir = Path.Combine(startupDir, "Streamer");
            var binaryPath = Path.Combine(streamerDir, AppConstants.StreamerFileName);
            var zipPath = Path.Combine(startupDir, AppConstants.StreamerZipFileName);

            if (_fileSystem.FileExists(zipPath))
            {
                while (true)
                {
                    var archiveCheckResult = await IsRemoteHashDifferent(zipPath);
                    if (!archiveCheckResult.IsSuccess)
                    {
                        return false;
                    }

                    if (!archiveCheckResult.Value && _fileSystem.FileExists(binaryPath))
                    {
                        return true;
                    }

                    var patchResult = await TryPatchCurrentVersion(zipPath, streamerDir);
                    if (patchResult)
                    {
                        // We may need multiple patches to get to latest version,
                        // so we'll iterate until the current zip hash matches the remote.
                        continue;
                    }
                    // Else, we'll break and continue with full upgrade.
                    break;
                }

                _logger.LogWarning("Patching failed.  Attempting full upgrade.");

                if (_fileSystem.DirectoryExists(streamerDir))
                {
                    _fileSystem.DeleteDirectory(streamerDir, true);
                }
            }
            else if (_fileSystem.DirectoryExists(streamerDir))
            {
                // If the archive doesn't exist, clear out any remaining files.
                // Then future update checks will work normally.
                _fileSystem.DeleteDirectory(streamerDir, true);
            }

            if (!_fileSystem.FileExists(binaryPath))
            {
                _previousProgress = 0;
                var downloadResult = await DownloadStreamer(streamerDir);
                if (!downloadResult)
                {
                    return downloadResult;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            var result = Result.Fail(ex, "Error while ensuring remote control latest version.");
            _logger.LogResult(result);
            return false;
        }
        finally
        {
            _updateLock.Release();
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

    private async Task<bool> DownloadStreamer(string streamerDir)
    {
        try
        {
            var targetPath = Path.Combine(_environmentHelper.StartupDirectory, AppConstants.StreamerZipFileName);
            var result = await _downloadsApi.DownloadStreamerZip(targetPath, _streamerZipUri, async progress =>
            {
                await ReportDownloadProgress(progress, "Downloading streamer on remote device");
            });

            if (!result.IsSuccess)
            {
                return false;
            }

            await ReportDownloadProgress(-1, "Extracting streamer archive");

            ZipFile.ExtractToDirectory(targetPath, streamerDir);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while extracting remote control archive.");
            return false;
        }
    }

    private async Task<Result<bool>> IsRemoteHashDifferent(string zipPath)
    {
        byte[] localHash = [];

        using (var zipFs = _fileSystem.OpenFileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            localHash = await MD5.HashDataAsync(zipFs);
        }

        _logger.LogInformation("Checking streamer remote archive hash.");
        var streamerHashResult = await _versionApi.GetCurrentStreamerHash(_environmentHelper.Runtime);
        if (!streamerHashResult.IsSuccess)
        {
            _logger.LogResult(streamerHashResult);
            return streamerHashResult.ToResult(false);
        }

        _logger.LogInformation(
            "Comparing local streamer archive hash ({LocalArchiveHash}) to remote ({RemoteArchiveHash}).",
            Convert.ToBase64String(localHash),
            Convert.ToBase64String(streamerHashResult.Value));

        if (streamerHashResult.Value.SequenceEqual(localHash))
        {
            _logger.LogInformation("Versions match.  Continuing.");
            return Result.Ok(false);
        }
        else
        {
            _logger.LogInformation("Versions differ.  Proceeding with update.");
            return Result.Ok(true);
        }
    }
    private async Task ReportDownloadProgress(double progress, string message)
    {
        var connection = _serviceProvider.GetRequiredService<IAgentHubConnection>();

        if (progress == 1 || progress < 0 || progress - _previousProgress > .05)
        {
            _previousProgress = progress;

            foreach (var request in _pendingRequests)
            {
                try
                {
                    var dto = new StreamerDownloadProgressDto(
                        request.SessionId,
                        request.ViewerConnectionId,
                        progress,
                        message);

                    await connection
                        .SendStreamerDownloadProgress(dto)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while sending remote control download progress.");
                }
            }
        }
    }

    private async Task<bool> TryPatchCurrentVersion(string streamerZipPath, string streamerDir)
    {
        try
        {
            _logger.LogInformation("Checking for differential patch for current version.");
            await ReportDownloadProgress(-1, "Attempting to patch current version");

            var tempStreamerPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(streamerZipPath));

            using (var basisStream = _fileSystem.OpenFileStream(streamerZipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var hash = await MD5.HashDataAsync(basisStream);
                var hexValue = Convert.ToHexString(hash);
                var deltaFileName = $"windows-streamer-{hexValue}.octodelta";
                var deltaPath = Path.Combine(Path.GetTempPath(), deltaFileName);
                var downloadUri = $"{_settings.ServerUri}downloads/deltas/{deltaFileName}";
                var downloadResult = await _downloadsApi.DownloadFile(downloadUri, deltaPath);

                if (!downloadResult.IsSuccess)
                {
                    return false;
                }

                _logger.LogInformation("Downloaded patch.  Applying.");
                var deltaApplier = new DeltaApplier { SkipHashCheck = false };

                basisStream.Seek(0, SeekOrigin.Begin);

           
                using var deltaStream = _fileSystem.OpenFileStream(deltaPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var newFileStream = _fileSystem.OpenFileStream(tempStreamerPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                deltaApplier.Apply(basisStream, new BinaryDeltaReader(deltaStream, _progressReporter), newFileStream);
                _logger.LogInformation("Patch applied.");
            }

            _logger.LogInformation("Extracting patched archive.");
            await ReportDownloadProgress(-1, "Extracting patched archive");

            if (_fileSystem.DirectoryExists(streamerDir))
            {
                _fileSystem.DeleteDirectory(streamerDir, true);
            }
            _fileSystem.CreateDirectory(streamerDir);
            _fileSystem.MoveFile(tempStreamerPath, streamerZipPath, true);
            ZipFile.ExtractToDirectory(streamerZipPath, streamerDir);

            _logger.LogInformation("Patching completed.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while attempting to patch streamer.");
            return false;
        }
    }
}
