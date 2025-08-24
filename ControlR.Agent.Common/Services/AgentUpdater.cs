using System.Security.Cryptography;
using System.Web.Services.Description;
using ControlR.Libraries.DevicesCommon.Options;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services;

internal interface IAgentUpdater : IHostedService
{
  ManualResetEventAsync UpdateCheckCompletedSignal { get; }
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
  ILogger<AgentUpdater> logger) : BackgroundService, IAgentUpdater
{
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly SemaphoreSlim _checkForUpdatesLock = new(1, 1);
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly IDownloadsApi _downloadsApi = downloadsApi;
  private readonly ISystemEnvironment _environmentHelper = environmentHelper;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
  private readonly ILogger<AgentUpdater> _logger = logger;
  private readonly IProcessManager _processInvoker = processInvoker;
  private readonly ISettingsProvider _settings = settings;

  public ManualResetEventAsync UpdateCheckCompletedSignal { get; } = new();

  public async Task CheckForUpdate(CancellationToken cancellationToken = default)
  {
    if (_settings.DisableAutoUpdate)
    {
      _logger.LogInformation("Auto-update disabled in developer options.  Skipping update check.");
      return;
    }

    using var logScope = _logger.BeginMemberScope();

    using var updateCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
    using var linkedCts =
      CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _appLifetime.ApplicationStopping, updateCts.Token);

    if (!await _checkForUpdatesLock.WaitAsync(TimeSpan.FromSeconds(5), linkedCts.Token))
    {
      _logger.LogWarning("Failed to acquire lock in agent updater within 5 seconds. Another update check may be in progress.");
      return;
    }

    try
    {
      UpdateCheckCompletedSignal.Reset();

      _logger.LogInformation("Beginning version check.");

      var hashResult = await _controlrApi.GetCurrentAgentHash(_environmentHelper.Runtime);
      if (!hashResult.IsSuccess)
      {
        return;
      }

      var remoteHash = hashResult.Value;
      var serverOrigin = _settings.ServerUri.ToString().TrimEnd('/');
      var downloadPath = AppConstants.GetAgentFileDownloadPath(_environmentHelper.Runtime);
      var downloadUrl = $"{serverOrigin}{downloadPath}";

      await using var startupExeFs = _fileSystem.OpenFileStream(_environmentHelper.StartupExePath, FileMode.Open,
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

      var tempDirPath = string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId)
        ? Path.Combine(Path.GetTempPath(), "ControlR_Update")
        : Path.Combine(Path.GetTempPath(), "ControlR_Update", _instanceOptions.Value.InstanceId);

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

      _logger.LogInformation("Launching installer.");

      var tenantId = _settings.TenantId;
      var installCommand = $"install -t {tenantId}";
      if (_instanceOptions.Value.InstanceId is { } instanceId)
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
    catch (OperationCanceledException ex)
    {
      _logger.LogInformation(ex, "Timed out during the update check process.");
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