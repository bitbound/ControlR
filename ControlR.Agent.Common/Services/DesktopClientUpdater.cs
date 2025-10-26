using System.IO.Compression;
using System.Security.Cryptography;
using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Collections;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Services.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

internal class DesktopClientUpdater(
  IServiceProvider serviceProvider,
  IFileSystem fileSystem,
  IDownloadsApi downloadsApi,
  ISystemEnvironment systemEnvironment,
  IServiceControl serviceControl,
  IControlrApi controlrApi,
  ISettingsProvider settings,
  IAgentUpdater agentUpdater,
  IProcessManager processManager,
  ILogger<DesktopClientUpdater> logger) : BackgroundService, IDesktopClientUpdater
{
  private readonly IAgentUpdater _agentUpdater = agentUpdater;
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly IDownloadsApi _downloadsApi = downloadsApi;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ILogger<DesktopClientUpdater> _logger = logger;
  private readonly ConcurrentList<RemoteControlSessionRequestDto> _pendingRequests = [];
  private readonly IProcessManager _processManager = processManager;
  private readonly IServiceControl _serviceControl = serviceControl;
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  private readonly ISettingsProvider _settings = settings;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly SemaphoreSlim _updateLock = new(1, 1);

  private double _previousProgress;

  public async Task<bool> EnsureLatestVersion(RemoteControlSessionRequestDto requestDto, CancellationToken cancellationToken)
  {
    if (_settings.DisableAutoUpdate)
    {
      _logger.LogInformation("Auto-update disabled in developer options.  Skipping desktop client update check.");
      return false;
    }

    if (_systemEnvironment.Platform != SystemPlatform.Windows &&
        _systemEnvironment.Platform != SystemPlatform.MacOs &&
        _systemEnvironment.Platform != SystemPlatform.Linux)
    {
      _logger.LogInformation("Desktop client update check is only supported on Windows, MacOS, and Linux platforms. Current platform: {Platform}", _systemEnvironment.Platform);
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
      _logger.LogInformation("Ensuring latest version of desktop client.");

      var startupDir = _systemEnvironment.StartupDirectory;
      var desktopDir = Path.Combine(startupDir, "DesktopClient");
      var binaryPath = AppConstants.GetDesktopExecutablePath(startupDir);
      var zipPath = Path.Combine(startupDir, AppConstants.DesktopClientZipFileName);

      if (_fileSystem.FileExists(zipPath) &&
          _fileSystem.FileExists(binaryPath))
      {
        var archiveCheckResult = await IsRemoteHashDifferent(zipPath);

        if (!archiveCheckResult.IsSuccess)
        {
          return false;
        }

        // Version is current.
        if (archiveCheckResult is { IsSuccess: true, Value: false })
        {
          return true;
        }
      }

      var runningDesktopClients = _processManager.GetProcessesByName(
        Path.GetFileNameWithoutExtension(AppConstants.DesktopClientFileName));

      foreach (var process in runningDesktopClients)
      {
        _logger.LogInformation("Killing running desktop client process {ProcessId} ({ProcessName})", process.Id, process.ProcessName);
        process.KillAndDispose();
      }

      if (_fileSystem.DirectoryExists(desktopDir))
      {
        _fileSystem.DeleteDirectory(desktopDir, true);
      }

      return await DownloadDesktopClient(desktopDir);
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
      _logger.LogInformation("Auto-update disabled in developer options.  Skipping desktop client update check.");
      return;
    }

    if (_systemEnvironment.Platform != SystemPlatform.Windows &&
        _systemEnvironment.Platform != SystemPlatform.MacOs &&
        _systemEnvironment.Platform != SystemPlatform.Linux)
    {
      _logger.LogInformation("Desktop client update check is only supported on Windows, MacOS, and Linux platforms. Current platform: {Platform}", _systemEnvironment.Platform);
      return;
    }

    _ = await EnsureLatestVersion(stoppingToken);

    using var timer = new PeriodicTimer(TimeSpan.FromHours(6));

    while (await timer.WaitForNextTickAsync(stoppingToken))
    {
      _ = await EnsureLatestVersion(stoppingToken);
    }
  }

  private async Task<bool> DownloadDesktopClient(string desktopDir)
  {
    try
    {
      if (_systemEnvironment.Platform == SystemPlatform.MacOs || _systemEnvironment.Platform == SystemPlatform.Linux)
      {
        await _serviceControl.StopDesktopClientService(throwOnFailure: false);
      }

      _previousProgress = 0;
      var targetPath = Path.Combine(_systemEnvironment.StartupDirectory, AppConstants.DesktopClientZipFileName);
      _logger.LogInformation("Downloading desktop client archive to {Path}", targetPath);

      var result = await _downloadsApi.DownloadDesktopClientZip(
        targetPath,
        GetDesktopZipUri(),
        async progress =>
        {
          await ReportDownloadProgress(progress, "Downloading desktop client on remote device");
        });

      if (!result.IsSuccess)
      {
        return false;
      }

      await ReportDownloadProgress(-1, "Extracting desktop client archive");

      ZipFile.ExtractToDirectory(targetPath, desktopDir, true);

      await SetDesktopClientPermissions(desktopDir);

      if (_systemEnvironment.Platform == SystemPlatform.MacOs || _systemEnvironment.Platform == SystemPlatform.Linux)
      {
        await _serviceControl.StartDesktopClientService(throwOnFailure: false);
      }
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while extracting remote control archive.");
      return false;
    }
  }

  private string GetDesktopZipUri()
  {
    return _systemEnvironment.Runtime switch
    {
      RuntimeId.WinX64 => $"{_settings.ServerUri}downloads/win-x64/{AppConstants.DesktopClientZipFileName}",
      RuntimeId.WinX86 => $"{_settings.ServerUri}downloads/win-x86/{AppConstants.DesktopClientZipFileName}",
      RuntimeId.MacOsX64 => $"{_settings.ServerUri}downloads/osx-x64/{AppConstants.DesktopClientZipFileName}",
      RuntimeId.MacOsArm64 => $"{_settings.ServerUri}downloads/osx-arm64/{AppConstants.DesktopClientZipFileName}",
      RuntimeId.LinuxX64 => $"{_settings.ServerUri}downloads/linux-x64/{AppConstants.DesktopClientZipFileName}",
      _ => throw new PlatformNotSupportedException($"Unsupported runtime ID: {_systemEnvironment.Runtime}"),
    };
  }

  private async Task<Result<bool>> IsRemoteHashDifferent(string zipPath)
  {
    byte[] localHash;

    await using (var zipFs = _fileSystem.OpenFileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
      localHash = await SHA256.HashDataAsync(zipFs);
    }

    _logger.LogInformation("Checking desktop client remote archive hash.");
    var desktopClientHashResult = await _controlrApi.GetCurrentDesktopClientHash(_systemEnvironment.Runtime);
    if (!desktopClientHashResult.IsSuccess)
    {
      _logger.LogResult(desktopClientHashResult);
      return desktopClientHashResult.ToResult(false);
    }

    _logger.LogInformation(
      "Comparing local desktop client archive hash ({LocalArchiveHash}) to remote ({RemoteArchiveHash}).",
      Convert.ToHexString(localHash),
      Convert.ToHexString(desktopClientHashResult.Value));

    if (desktopClientHashResult.Value.SequenceEqual(localHash))
    {
      _logger.LogInformation("Versions match.  Continuing.");
      return Result.Ok(false);
    }

    _logger.LogInformation("Versions differ.  Proceeding with desktop client update.");
    return Result.Ok(true);
  }

  private async Task ReportDownloadProgress(double progress, string message)
  {
    var connection = _serviceProvider.GetRequiredService<IHubConnection<IAgentHub>>();

    if (progress >= 1 || progress < 0 || progress - _previousProgress > .05)
    {
      _previousProgress = progress;

      foreach (var request in _pendingRequests)
      {
        try
        {
          var dto = new DesktopClientDownloadProgressDto(
            request.SessionId,
            request.ViewerConnectionId,
            progress,
            message);

          await connection
            .Server
            .SendDesktopClientDownloadProgress(dto)
            .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error while sending remote control download progress.");
        }
      }
    }
  }

  private async Task SetDesktopClientPermissions(string desktopDir)
  {
    if (_systemEnvironment.Platform == SystemPlatform.MacOs)
    {
      // Ensure the Mac app bundle executable has correct permissions
      var appBundleExecutablePath = Path.Combine(desktopDir, "ControlR.app", "Contents", "MacOS", "ControlR.DesktopClient");
      if (_fileSystem.FileExists(appBundleExecutablePath))
      {
        var chmodResult = await _processManager.GetProcessOutput("chmod", "+x " + appBundleExecutablePath, 5000);
        if (!chmodResult.IsSuccess)
        {
          _logger.LogWarning("Failed to set executable permissions on Mac app bundle: {Error}", chmodResult.Reason);
        }
      }      
    }
    else if (_systemEnvironment.Platform == SystemPlatform.Linux)
    {
      // Ensure the Linux executable has correct permissions
      var linuxExecutablePath = Path.Combine(desktopDir, AppConstants.DesktopClientFileName);
      if (_fileSystem.FileExists(linuxExecutablePath))
      {
        var chmodResult = await _processManager.GetProcessOutput("chmod", "+x " + linuxExecutablePath, 5000);
        if (!chmodResult.IsSuccess)
        {
          _logger.LogWarning("Failed to set executable permissions on Linux executable: {Error}", chmodResult.Reason);
        }
      }
    }
  }
}