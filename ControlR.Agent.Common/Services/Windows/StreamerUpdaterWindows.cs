using System.IO.Compression;
using System.Security.Cryptography;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Options;
using ControlR.Libraries.Shared.Collections;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Services.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services.Windows;

internal class StreamerUpdaterWindows(
  IServiceProvider serviceProvider,
  IFileSystem fileSystem,
  IDownloadsApi downloadsApi,
  ISystemEnvironment environmentHelper,
  IControlrApi controlrApi,
  ISettingsProvider settings,
  IAgentUpdater agentUpdater,
  ILogger<StreamerUpdaterWindows> logger) : BackgroundService, IStreamerUpdater
{
  private readonly ConcurrentList<StreamerSessionRequestDto> _pendingRequests = [];
  private readonly string _streamerZipUri = $"{settings.ServerUri}downloads/win-x86/{AppConstants.StreamerZipFileName}";
  private readonly SemaphoreSlim _updateLock = new(1, 1);
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IDownloadsApi _downloadsApi = downloadsApi;
  private readonly ISystemEnvironment _environmentHelper = environmentHelper;
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly ISettingsProvider _settings = settings;
  private readonly IAgentUpdater _agentUpdater = agentUpdater;
  private readonly ILogger<StreamerUpdaterWindows> _logger = logger;
  private double _previousProgress;

  public async Task<bool> EnsureLatestVersion(StreamerSessionRequestDto requestDto, CancellationToken cancellationToken)
  {
    if (_settings.DisableAutoUpdate)
    {
      _logger.LogInformation("Auto-update disabled in developer options.  Skipping streamer update check.");
      return false;
    }

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

      if (_fileSystem.FileExists(zipPath) &&
          _fileSystem.FileExists(binaryPath))
      {
        var archiveCheckResult = await IsRemoteHashDifferent(zipPath);

        // Version is current.
        if (archiveCheckResult.IsSuccess && !archiveCheckResult.Value)
        {
          return true;
        }
      }

      if (_fileSystem.DirectoryExists(streamerDir))
      {
        _fileSystem.DeleteDirectory(streamerDir, true);
      }

      return await DownloadStreamer(streamerDir);
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
    if (_settings.DisableAutoUpdate)
    {
      _logger.LogInformation("Auto-update disabled in developer options.  Skipping streamer update check.");
      return;
    }

    _ = await EnsureLatestVersion(stoppingToken);

    using var timer = new PeriodicTimer(TimeSpan.FromHours(6));

    while (await timer.WaitForNextTickAsync(stoppingToken))
    {
      _ = await EnsureLatestVersion(stoppingToken);
    }
  }

  private async Task<bool> DownloadStreamer(string streamerDir)
  {
    try
    {
      _previousProgress = 0;
      var targetPath = Path.Combine(_environmentHelper.StartupDirectory, AppConstants.StreamerZipFileName);
      var result = await _downloadsApi.DownloadStreamerZip(targetPath, _streamerZipUri,
        async progress => { await ReportDownloadProgress(progress, "Downloading streamer on remote device"); });

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

    await using (var zipFs = _fileSystem.OpenFileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
      localHash = await SHA256.HashDataAsync(zipFs);
    }

    _logger.LogInformation("Checking streamer remote archive hash.");
    var streamerHashResult = await _controlrApi.GetCurrentStreamerHash(_environmentHelper.Runtime);
    if (!streamerHashResult.IsSuccess)
    {
      _logger.LogResult(streamerHashResult);
      return streamerHashResult.ToResult(false);
    }

    _logger.LogInformation(
      "Comparing local streamer archive hash ({LocalArchiveHash}) to remote ({RemoteArchiveHash}).",
      Convert.ToHexString(localHash),
      Convert.ToHexString(streamerHashResult.Value));

    if (streamerHashResult.Value.SequenceEqual(localHash))
    {
      _logger.LogInformation("Versions match.  Continuing.");
      return Result.Ok(false);
    }

    _logger.LogInformation("Versions differ.  Proceeding with update.");
    return Result.Ok(true);
  }

  private async Task ReportDownloadProgress(double progress, string message)
  {
    var connection = _serviceProvider.GetRequiredService<IHubConnection<IAgentHub>>();

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
            .Server
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
}