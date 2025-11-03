using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace ControlR.Agent.Common.Services;

internal interface IAgentUpdater : IHostedService
{
  Task CheckForUpdate(CancellationToken cancellationToken = default);
}

internal class AgentUpdater(
  TimeProvider timeProvider,
  IControlrApi controlrApi,
  IDownloadsApi downloadsApi,
  IFileSystem fileSystem,
  IProcessManager processInvoker,
  ISystemEnvironment environmentHelper,
  ISettingsProvider settings,
  IHostApplicationLifetime appLifetime,
  IOptions<InstanceOptions> instanceOptions,
  IControlrMutationLock mutationLock,
  ILogger<AgentUpdater> logger) : BackgroundService, IAgentUpdater
{
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly IDownloadsApi _downloadsApi = downloadsApi;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
  private readonly ILogger<AgentUpdater> _logger = logger;
  private readonly IControlrMutationLock _mutationLock = mutationLock;
  private readonly IProcessManager _processInvoker = processInvoker;
  private readonly ISettingsProvider _settings = settings;
  private readonly ISystemEnvironment _systemEnvironment = environmentHelper;
  private readonly TimeProvider _timeProvider = timeProvider;
  
  public async Task CheckForUpdate(CancellationToken cancellationToken = default)
  {
    if (_settings.DisableAutoUpdate)
    {
      _logger.LogInformation("Auto-update disabled in developer options.  Skipping update check.");
      return;
    }

    using var logScope = _logger.BeginMemberScope();

    using var updateCts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
        cancellationToken,
        _appLifetime.ApplicationStopping,
        updateCts.Token);

    // Try to acquire the global mutation lock with a short timeout to avoid long waits during periodic checks.
    var lockHandle = await _mutationLock.TryAcquireAsync(TimeSpan.FromSeconds(5), linkedCts.Token);
    if (lockHandle is null)
    {
      _logger.LogWarning("Skipped check: mutation lock not acquired within 5 seconds (another mutation may be in progress).");
      return;
    }

    try
    {
      using (lockHandle)
      {
        _logger.LogInformation("Beginning version check.");

      // Get remote hash
      var hashResult = await _controlrApi.GetCurrentAgentHashSha256(_systemEnvironment.Runtime, linkedCts.Token);
      if (!hashResult.IsSuccess)
      {
        _logger.LogResult(hashResult);
        return;
      }

      var remoteHash = hashResult.Value;
      _logger.LogInformation("Remote hash: {RemoteHash}", remoteHash);

      // Check local hash
      var agentPath = _systemEnvironment.StartupExePath;
      if (string.IsNullOrWhiteSpace(agentPath) || !_fileSystem.FileExists(agentPath))
      {
        _logger.LogError("Could not determine current agent file path.");
        return;
      }

      await using var fs = _fileSystem.OpenFileStream(agentPath, FileMode.Open, FileAccess.Read, FileShare.Read);
      var localHashBytes = await SHA256.HashDataAsync(fs, linkedCts.Token);
      var localHash = Convert.ToHexString(localHashBytes);
      _logger.LogInformation("Local hash: {LocalHash}", localHash);

      // Compare hashes
      if (string.Equals(localHash, remoteHash, StringComparison.OrdinalIgnoreCase))
      {
        _logger.LogInformation("Version is current (hash match).");
        return;
      }

      _logger.LogInformation("Update found. Downloading update.");

      var downloadPath = AppConstants.GetAgentFileDownloadPath(_systemEnvironment.Runtime);
      var downloadUrl = new Uri(_settings.ServerUri, downloadPath).ToString();

      var tempDirPath = string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId)
        ? Path.Combine(Path.GetTempPath(), "ControlR_Update")
        : Path.Combine(Path.GetTempPath(), "ControlR_Update", _instanceOptions.Value.InstanceId);

      _ = _fileSystem.CreateDirectory(tempDirPath);
      var tempPath = Path.Combine(tempDirPath, AppConstants.GetAgentFileName(_systemEnvironment.Platform));

      if (_fileSystem.FileExists(tempPath))
      {
        _fileSystem.DeleteFile(tempPath);
      }

      var result = await _downloadsApi.DownloadFile(downloadUrl, tempPath, linkedCts.Token);
      if (!result.IsSuccess)
      {
        _logger.LogCritical("Download failed.  Aborting update.");
        return;
      }

      _logger.LogInformation("Launching installer.");

      var tenantId = _settings.TenantId;
      var installCommand = $"install -t {tenantId}";
      if (_instanceOptions.Value.InstanceId is { } instanceId)
      {
        installCommand += $" -i {instanceId}";
      }

        switch (_systemEnvironment.Platform)
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

          case SystemPlatform.MacOs:
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
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogInformation(ex, "Timed out during the update check process.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while checking for updates.");
    }
  }



  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (_settings.DisableAutoUpdate)
    {
      _logger.LogInformation("Auto-update disabled in developer options.  Skipping update check timer.");
      return;
    }

    await CheckForUpdate(stoppingToken);

    using var timer = new PeriodicTimer(TimeSpan.FromHours(6), _timeProvider);

    while (await timer.WaitForNextTickAsync(stoppingToken))
    {
      await CheckForUpdate(stoppingToken);
    }
  }
}