using System.Security.Cryptography;
using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Shared.Constants;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

internal class DesktopClientUpdater(
  IFileSystem fileSystem,
  ISystemEnvironment systemEnvironment,
  IServiceControl serviceControl,
  ISettingsProvider settings,
  IControlrMutationLock mutationLock,
  IProcessManager processManager,
  IDesktopSessionProvider desktopSessionProvider,
  IEmbeddedDesktopClientProvider embeddedDesktopClientProvider,
  ILogger<DesktopClientUpdater> logger) : BackgroundService, IDesktopClientUpdater
{
  private readonly IDesktopSessionProvider _desktopSessionProvider = desktopSessionProvider;
  private readonly IEmbeddedDesktopClientProvider _embeddedDesktopClientProvider = embeddedDesktopClientProvider;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ILogger<DesktopClientUpdater> _logger = logger;
  private readonly IControlrMutationLock _mutationLock = mutationLock;
  private readonly IProcessManager _processManager = processManager;
  private readonly IServiceControl _serviceControl = serviceControl;
  private readonly ISettingsProvider _settings = settings;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly SemaphoreSlim _updateLock = new(1, 1);


  public async Task<bool> EnsureLatestVersion(CancellationToken cancellationToken)
  {
    if (_settings.DisableAutoUpdate)
    {
      _logger.LogInformation("Auto-update disabled in developer options.  Skipping desktop client update check.");
      return false;
    }

    using var mutation = await _mutationLock.AcquireAsync(cancellationToken);
    await _updateLock.WaitAsync(cancellationToken);
    try
    {
      _logger.LogInformation("Ensuring latest version of desktop client.");

      var startupDir = _systemEnvironment.StartupDirectory;
      var desktopDir = Path.Combine(startupDir, "DesktopClient");
      var binaryPath = AppConstants.GetDesktopExecutablePath(startupDir);
      var extractedZipPath = Path.Combine(startupDir, AppConstants.DesktopClientZipFileName);

      // 1) Compute embedded zip hash
      var embeddedHashResult = await GetEmbeddedZipHash(cancellationToken);
      if (!embeddedHashResult.IsSuccess)
      {
        return false;
      }
      var embeddedHash = embeddedHashResult.Value;

      // 2) Compute extracted zip hash (if present)
      var extractedHash = await GetFileHash(extractedZipPath, cancellationToken);

      // 3) Quick up-to-date check
      if (IsUpToDate(extractedHash, embeddedHash, binaryPath))
      {
        _logger.LogInformation("Desktop client version is current (hash match).");
        return true;
      }

      _logger.LogInformation("Desktop client update required. Extracting from embedded resources.");

      // 4) Disconnect active desktop clients
      await DisconnectActiveDesktopClients();

      // 5) Clean existing desktop directory
      if (_fileSystem.DirectoryExists(desktopDir))
      {
        _fileSystem.DeleteDirectory(desktopDir, true);
      }

      // 6) Extract
      var extractResult = await ExtractDesktopClient(desktopDir);
      if (extractResult)
      {
        _logger.LogInformation("Desktop client updated successfully.");
      }
      else
      {
        _logger.LogError("Desktop client update failed.");
      }

      return extractResult;
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

  private async Task DisconnectActiveDesktopClients()
  {
    var desktopSessions = await _desktopSessionProvider.GetActiveDesktopClients();
    foreach (var session in desktopSessions)
    {
      try
      {
        _logger.LogInformation("Disconnecting active desktop client process {ProcessId}.", session.ProcessId);
        _processManager
          .GetProcessById(session.ProcessId)
          .KillAndDispose();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to disconnect desktop client process {ProcessId}.", session.ProcessId);
      }
    }
  }

  private async Task<bool> ExtractDesktopClient(string desktopDir)
  {
    try
    {
      if (_systemEnvironment.Platform == SystemPlatform.MacOs || _systemEnvironment.Platform == SystemPlatform.Linux)
      {
        await _serviceControl.StopDesktopClientService(throwOnFailure: false);
      }

      var targetPath = Path.Combine(_systemEnvironment.StartupDirectory, AppConstants.DesktopClientZipFileName);
      _logger.LogInformation("Extracting desktop client archive to {Path}", targetPath);

      var extractResult = await _embeddedDesktopClientProvider.ExtractDesktopClient(targetPath, CancellationToken.None);

      if (!extractResult.IsSuccess)
      {
        _logger.LogError("Failed to extract embedded desktop client: {Reason}", extractResult.Reason);
        return false;
      }

      _fileSystem.ExtractZipArchive(targetPath, desktopDir, true);

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

  private async Task<Result<string>> GetEmbeddedZipHash(CancellationToken cancellationToken)
  {
    var embeddedHash = await _embeddedDesktopClientProvider.GetEmbeddedResourceHash(cancellationToken);
    if (!embeddedHash.IsSuccess)
    {
      _logger.LogError("Failed to compute hash of embedded desktop client: {Reason}", embeddedHash.Reason);
    }
    else
    {
      _logger.LogInformation("Embedded desktop client hash: {EmbeddedHash}", embeddedHash.Value);
    }
    return embeddedHash;
  }

  private async Task<string?> GetFileHash(string path, CancellationToken cancellationToken)
  {
    if (!_fileSystem.FileExists(path))
    {
      _logger.LogInformation("Extracted desktop client ZIP not found. Update required.");
      return null;
    }

    try
    {
      await using var fileStream = _fileSystem.OpenFileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
      var hashBytes = await SHA256.HashDataAsync(fileStream, cancellationToken);
      var extractedHash = Convert.ToHexString(hashBytes);
      _logger.LogInformation("Extracted desktop client hash: {ExtractedHash}", extractedHash);
      return extractedHash;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to compute hash of extracted desktop client ZIP. Will re-extract.");
      return null;
    }
  }


  private bool IsUpToDate(string? extractedHash, string embeddedHash, string binaryPath)
  {
    return !string.IsNullOrWhiteSpace(extractedHash)
           && string.Equals(extractedHash, embeddedHash, StringComparison.OrdinalIgnoreCase)
           && _fileSystem.FileExists(binaryPath);
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