using System.Security.Cryptography;
using ControlR.Agent.Options;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Services;

internal interface IAgentUpdater : IHostedService
{
  ManualResetEventAsync UpdateCheckCompletedSignal { get; }
  Task CheckForUpdate(CancellationToken cancellationToken = default);
}

internal class AgentUpdater(
  IVersionApi versionApi,
  IDownloadsApi downloadsApi,
  IReleasesApi releasesApi,
  IFileSystem fileSystem,
  IProcessManager processInvoker,
  ISystemEnvironment environmentHelper,
  ISettingsProvider settings,
  IHostApplicationLifetime appLifetime,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<AgentUpdater> logger) : BackgroundService, IAgentUpdater
{
  private readonly SemaphoreSlim _checkForUpdatesLock = new(1, 1);
  private readonly ILogger<AgentUpdater> _logger = logger;

  public ManualResetEventAsync UpdateCheckCompletedSignal { get; } = new();

  public async Task CheckForUpdate(CancellationToken cancellationToken = default)
  {
    if (environmentHelper.IsDebug)
    {
      return;
    }

    using var logScope = _logger.BeginMemberScope();

    using var linkedCts =
      CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, appLifetime.ApplicationStopping);

    if (!await _checkForUpdatesLock.WaitAsync(0, linkedCts.Token))
    {
      _logger.LogWarning("Failed to acquire lock in agent updater.  Aborting check.");
      return;
    }

    try
    {
      UpdateCheckCompletedSignal.Reset();

      _logger.LogInformation("Beginning version check.");


      var hashResult = await versionApi.GetCurrentAgentHash(environmentHelper.Runtime);
      if (!hashResult.IsSuccess)
      {
        return;
      }

      var remoteHash = hashResult.Value;
      var serverOrigin = settings.ServerUri.ToString().TrimEnd('/');
      var downloadPath = AppConstants.GetAgentFileDownloadPath(environmentHelper.Runtime);
      var downloadUrl = $"{serverOrigin}{downloadPath}";

      await using var startupExeFs = fileSystem.OpenFileStream(environmentHelper.StartupExePath, FileMode.Open,
        FileAccess.Read, FileShare.Read);
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

      var tempDirPath = string.IsNullOrWhiteSpace(instanceOptions.Value.InstanceId)
        ? Path.Combine(Path.GetTempPath(), "ControlR_Update")
        : Path.Combine(Path.GetTempPath(), "ControlR_Update", instanceOptions.Value.InstanceId);

      _ = fileSystem.CreateDirectory(tempDirPath);
      var tempPath = Path.Combine(tempDirPath, AppConstants.GetAgentFileName(environmentHelper.Platform));

      if (fileSystem.FileExists(tempPath))
      {
        fileSystem.DeleteFile(tempPath);
      }

      var result = await downloadsApi.DownloadFile(downloadUrl, tempPath);
      if (!result.IsSuccess)
      {
        _logger.LogCritical("Download failed.  Aborting update.");
        return;
      }

      await using (var tempFs = fileSystem.OpenFileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
      {
        var updateHash = await SHA256.HashDataAsync(tempFs, linkedCts.Token);
        var updateHexHash = Convert.ToHexString(updateHash);

        if (settings.IsConnectedToPublicServer &&
            !await releasesApi.DoesReleaseHashExist(updateHexHash))
        {
          _logger.LogCritical(
            "A new agent version is available, but the hash does not exist in the public releases data.");
          return;
        }
      }

      _logger.LogInformation("Launching installer.");

      var installCommand = "install";
      if (instanceOptions.Value.InstanceId is string instanceId)
      {
        installCommand += $" -i {instanceId}";
      }

      switch (environmentHelper.Platform)
      {
        case SystemPlatform.Windows:
        {
          await processInvoker
            .Start(tempPath, installCommand)
            .WaitForExitAsync(linkedCts.Token);
        }
          break;

        case SystemPlatform.Linux:
        {
          await processInvoker
            .Start("sudo", $"chmod +x {tempPath}")
            .WaitForExitAsync(linkedCts.Token);

          await processInvoker.StartAndWaitForExit(
            "/bin/bash",
            $"-c \"{tempPath} {installCommand} &\"",
            true,
            linkedCts.Token);
        }
          break;

        case SystemPlatform.MacOs:
        {
          await processInvoker
            .Start("sudo", $"chmod +x {tempPath}")
            .WaitForExitAsync(linkedCts.Token);

          await processInvoker.StartAndWaitForExit(
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
    if (environmentHelper.IsDebug)
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