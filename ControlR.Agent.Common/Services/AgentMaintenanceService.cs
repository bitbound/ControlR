using ControlR.Libraries.Shared.Logging;
using ControlR.Libraries.Branding;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Http;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ControlR.Agent.Common.Services;

/// <summary>
/// Coordinates installer-based maintenance operations for the agent, including update checks and desktop client repair.
/// </summary>
internal interface IAgentMaintenanceService : IHostedService
{
  /// <summary>
  /// Checks for updates to the ControlR agent.
  /// </summary>
  /// <param name="force">Whether to force an update check, bypassing DisableAutoUpdate in developer settings.</param>
  /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  Task CheckForUpdate(bool force = false, CancellationToken cancellationToken = default);

  /// <summary>
  /// Downloads and launches the platform installer to repair the installed desktop client payload.
  /// </summary>
  /// <param name="reason">The health or runtime reason that triggered the repair request.</param>
  /// <param name="cancellationToken">A cancellation token to cancel the repair operation.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  Task RepairDesktopClient(string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// Coordinates installer-based maintenance operations for the agent, including update checks and desktop client repair.
/// </summary>
internal class AgentMaintenanceService(
  TimeProvider timeProvider,
  IControlrApi controlrApi,
  IDownloadsApi downloadsApi,
  IFileSystem fileSystem,
  IFileSystemPathProvider fileSystemPathProvider,
  IProcessManager proessManager,
  ISystemEnvironment environmentHelper,
  IOptionsAccessor optionsAccessor,
  IHostApplicationLifetime appLifetime,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<AgentMaintenanceService> logger) : BackgroundService, IAgentMaintenanceService
{
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly IDownloadsApi _downloadsApi = downloadsApi;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IFileSystemPathProvider _fileSystemPathProvider = fileSystemPathProvider;
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
  private readonly ILogger<AgentMaintenanceService> _logger = logger;
  private readonly IOptionsAccessor _optionsAccessor = optionsAccessor;
  private readonly IProcessManager _processManager = proessManager;
  private readonly ISystemEnvironment _systemEnvironment = environmentHelper;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task CheckForUpdate(bool force = false, CancellationToken cancellationToken = default)
  {
    if (!force && _optionsAccessor.DisableAutoUpdate)
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

    try
    {
      _logger.LogInformation("Beginning version check.");

      var metadataResult = await _controlrApi.Agent.Updates.GetBundleMetadata(_systemEnvironment.Runtime, linkedCts.Token);
      if (!metadataResult.IsSuccess || metadataResult.Value is null)
      {
        _logger.LogErrorDeduped(
          "Failed to retrieve bundle metadata. Reason: {Reason}, StatusCode: {StatusCode}",
          args: [metadataResult.Reason, metadataResult.StatusCode]);
        return;
      }

      var metadata = metadataResult.Value;

      var isSameBrand = string.Equals(metadata.BrandName, BrandingConstants.BrandName, StringComparison.Ordinal);

      if (isSameBrand && !string.Equals(metadata.Publisher, BrandingConstants.Publisher, StringComparison.Ordinal))
      {
        _logger.LogCritical(
          "Aborting update check. Server bundle carries this agent's brand name but a different publisher. " +
          "Agent brand: {AgentBrandName}/{AgentPublisher}, Server brand: {ServerBrandName}/{ServerPublisher}.",
          BrandingConstants.BrandName,
          BrandingConstants.Publisher,
          metadata.BrandName,
          metadata.Publisher);
        return;
      }

      // The server is authoritative and its URL does not change across a rebrand, so a bundle that
      // names a different brand is this deployment rebranding itself. Migrate onto it.
      var isBrandMigration = !isSameBrand;
      if (isBrandMigration)
      {
        _logger.LogWarning(
          "Server bundle is brand {ServerBrandName}. This agent is brand {AgentBrandName}. Migrating this install.",
          metadata.BrandName,
          BrandingConstants.BrandName);

        // The device identity has to be in place before the new installer reads its own settings.
        // Without it that install registers a fresh device and signs with a key the server has never
        // been told about, and the existing device record can never authenticate again, so refuse
        // rather than try.
        if (!WriteSettingsFileForBrand(metadata.BrandName))
        {
          _logger.LogCritical(
            "Refusing to migrate. The settings file for brand {ServerBrandName} could not be prepared, " +
            "so this device's signing key would not carry over. Nothing on this machine was changed.",
            metadata.BrandName);
          return;
        }
      }

      _logger.LogInformation("Remote bundle hash: {RemoteHash}", metadata.BundleSha256);

      var localHash = GetInstalledBundleHash();
      if (!string.IsNullOrWhiteSpace(localHash))
      {
        _logger.LogInformation("Installed bundle hash: {LocalHash}", localHash);
      }

      // A migration always runs the installer, even if the hashes happen to agree, because the
      // install still has to move onto the new brand's service and directory names.
      if (!isBrandMigration && string.Equals(localHash, metadata.BundleSha256, StringComparison.OrdinalIgnoreCase))
      {
        _logger.LogInformation("Version is current (hash match).");
        return;
      }

      _logger.LogInformation("Update found. Downloading bootstrap installer.");

      var installerPath = await DownloadInstaller(metadata, linkedCts.Token);
      if (installerPath is null)
      {
        return;
      }

      _logger.LogInformation("Launching installer.");

      var installArguments = BuildInstallArguments(isBrandMigration);
      var installCommand = BuildCommandString(installArguments);
      await LaunchInstaller(installerPath, installCommand, installArguments, linkedCts.Token);
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

  public async Task RepairDesktopClient(string reason, CancellationToken cancellationToken = default)
  {
    using var logScope = _logger.BeginMemberScope();
    using var repairCts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
      cancellationToken,
      _appLifetime.ApplicationStopping,
      repairCts.Token);

    try
    {
      _logger.LogWarning("Desktop client repair requested. Reason: {Reason}", reason);

      var metadataResult = await _controlrApi.Agent.Updates.GetBundleMetadata(_systemEnvironment.Runtime, linkedCts.Token);
      if (!metadataResult.IsSuccess || metadataResult.Value is null)
      {
        _logger.LogError(
          "Failed to retrieve bundle metadata for desktop repair. Reason: {Reason}, StatusCode: {StatusCode}",
          metadataResult.Reason,
          metadataResult.StatusCode);
        return;
      }

      var metadata = metadataResult.Value;

      // Repair rewrites the desktop client payload inside an existing install, so it only makes
      // sense for the install's own brand. A cross-brand bundle is handled by the update path,
      // which moves the whole install rather than patching one payload into the wrong brand.
      if (!string.Equals(metadata.BrandName, BrandingConstants.BrandName, StringComparison.Ordinal))
      {
        _logger.LogCritical(
          "Aborting desktop client repair. Server bundle is not this agent's brand. " +
          "Agent brand: {AgentBrandName}/{AgentPublisher}, Server brand: {ServerBrandName}/{ServerPublisher}. " +
          "Refusing to write a foreign brand's payload into this install.",
          BrandingConstants.BrandName,
          BrandingConstants.Publisher,
          metadata.BrandName,
          metadata.Publisher);
        return;
      }

      var installerPath = await DownloadInstaller(metadata, linkedCts.Token);
      if (installerPath is null)
      {
        return;
      }

      var repairArguments = BuildRepairDesktopArguments();
      var repairCommand = BuildCommandString(repairArguments);
      await LaunchInstaller(installerPath, repairCommand, repairArguments, linkedCts.Token);
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogInformation(ex, "Timed out during the desktop repair process.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while repairing the desktop client.");
    }
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (_optionsAccessor.DisableAutoUpdate)
    {
      _logger.LogInformation("Auto-update disabled in developer options.  Skipping update check timer.");
      return;
    }

    await CheckForUpdate(cancellationToken: stoppingToken);

    using var timer = new PeriodicTimer(TimeSpan.FromHours(6), _timeProvider);

    while (await timer.WaitForNextTickAsync(stoppingToken))
    {
      await CheckForUpdate(cancellationToken: stoppingToken);
    }
  }

  private static string BuildCommandString(IEnumerable<string> arguments)
  {
    return string.Join(" ", arguments.Select(QuoteArgument));
  }

  private static string GetInstallerFileName(string downloadPath)
  {
    if (Uri.TryCreate(downloadPath, UriKind.RelativeOrAbsolute, out var uri))
    {
      var sourcePath = uri.IsAbsoluteUri ? uri.LocalPath : uri.OriginalString;
      var fileName = Path.GetFileName(sourcePath);
      if (!string.IsNullOrWhiteSpace(fileName))
      {
        return fileName;
      }
    }

    throw new InvalidOperationException($"Installer download path '{downloadPath}' does not contain a file name.");
  }

  private static string GetMacInstallerDaemonJobLabel(string? instanceId)
  {
    if (string.IsNullOrWhiteSpace(instanceId))
    {
      return $"{BrandingConstants.MacServicePrefix}.agent.installer";
    }

    return $"{BrandingConstants.MacServicePrefix}.agent.installer.{instanceId}";
  }

  private static string GetMacInstallerDaemonPlistPath(string? instanceId)
  {
    return string.IsNullOrWhiteSpace(instanceId)
      ? $"/Library/LaunchDaemons/{BrandingConstants.MacServicePrefix}.agent.installer.plist"
      : $"/Library/LaunchDaemons/{BrandingConstants.MacServicePrefix}.agent.installer.{instanceId}.plist";
  }

  private static ProcessStartInfo GetMacLaunchctlStartInfo(params string[] arguments)
  {
    var psi = new ProcessStartInfo
    {
      FileName = "sudo",
      UseShellExecute = false,
    };

    foreach (var argument in arguments)
    {
      psi.ArgumentList.Add(argument);
    }

    return psi;
  }

  private static string QuoteArgument(string value)
  {
    var escapedValue = value.Replace("\"", "\\\"");
    return $"\"{escapedValue}\"";
  }

  private List<string> BuildInstallArguments(bool isBrandMigration = false)
  {
    var arguments = new List<string>
    {
      "install",
      "--server-uri",
      _optionsAccessor.ServerUri.ToString(),
      "--tenant-id",
      _optionsAccessor.GetRequiredTenantId().ToString()
    };

    if (!string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      arguments.Add("--instance-id");
      arguments.Add(_instanceOptions.Value.InstanceId);
    }

    if (isBrandMigration)
    {
      // The new installer picks the install identity up from the settings file this agent already
      // wrote into that brand's settings directory, so only the brand being retired has to travel.
      arguments.Add("--previous-brand-name");
      arguments.Add(BrandingConstants.BrandName);
    }

    return arguments;
  }

  private List<string> BuildRepairDesktopArguments()
  {
    var arguments = new List<string>
    {
      "repair-desktop"
    };

    if (!string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      arguments.Add("--instance-id");
      arguments.Add(_instanceOptions.Value.InstanceId);
    }

    return arguments;
  }

  private async Task<string?> DownloadInstaller(BundleMetadataDto metadata, CancellationToken cancellationToken)
  {
    var tempDirPath = string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId)
      ? Path.Combine(Path.GetTempPath(), BrandingConstants.UpdaterTempDirectoryName)
      : Path.Combine(Path.GetTempPath(), BrandingConstants.UpdaterTempDirectoryName, _instanceOptions.Value.InstanceId);

    _ = _fileSystem.CreateDirectory(tempDirPath);

    var installerPath = _systemEnvironment.Platform == SystemPlatform.MacOs
      ? GetMacInstallerDownloadPath()
      : Path.Combine(tempDirPath, GetInstallerFileName(metadata.InstallerDownloadUrl));

    var installerDir = Path.GetDirectoryName(installerPath);
    if (installerDir is null)
    {
      _logger.LogCritical("Failed to determine installer directory from path: {InstallerPath}", installerPath);
      return null;
    }
    _ = _fileSystem.CreateDirectory(installerDir);

    if (_fileSystem.FileExists(installerPath))
    {
      _fileSystem.DeleteFile(installerPath);
    }

    var downloadResult = await _downloadsApi.DownloadFile(metadata.InstallerDownloadUrl, installerPath, cancellationToken);
    if (!downloadResult.IsSuccess)
    {
      _logger.LogCritical(
        "Failed to download installer from {DownloadUrl}. Reason: {Reason}",
        metadata.InstallerDownloadUrl,
        downloadResult.Reason);
      return null;
    }

    var installerBytes = await _fileSystem.ReadAllBytesAsync(installerPath);
    var installerHash = Convert.ToHexString(SHA256.HashData(installerBytes));
    if (!string.Equals(installerHash, metadata.InstallerSha256, StringComparison.OrdinalIgnoreCase))
    {
      _logger.LogCritical(
        "Installer hash mismatch. Expected: {ExpectedHash}, Actual: {ActualHash}",
        metadata.InstallerSha256,
        installerHash);
      _fileSystem.DeleteFile(installerPath);
      return null;
    }

    return installerPath;
  }

  private string? GetInstalledBundleHash()
  {
    var bundleHashPath = _fileSystemPathProvider.GetBundleHashFilePath();
    if (!_fileSystem.FileExists(bundleHashPath))
    {
      _logger.LogInformation("Installed bundle hash file was not found at {HashPath}.", bundleHashPath);
      return null;
    }

    var installedHash = _fileSystem.ReadAllText(bundleHashPath).Trim();
    return string.IsNullOrWhiteSpace(installedHash) ? null : installedHash;
  }

  private string GetMacInstallerDownloadPath()
  {
    var instanceSegment = string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId)
      ? AppConstants.DefaultInstanceId
      : _instanceOptions.Value.InstanceId;

    return _fileSystem.JoinPaths(
      Path.DirectorySeparatorChar,
      Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
      BrandingConstants.UpdaterTempDirectoryName,
      instanceSegment,
      AppConstants.GetInstallerFileName(SystemPlatform.MacOs));
  }

  private async Task LaunchInstaller(
    string installerPath,
    string installCommand,
    IReadOnlyList<string> installArguments,
    CancellationToken cancellationToken)
  {
    switch (_systemEnvironment.Platform)
    {
      case SystemPlatform.Windows:
        await _processManager
          .Start(installerPath, installCommand)
          .WaitForExitAsync(cancellationToken);
        break;

      case SystemPlatform.Linux:
        await _processManager
          .Start("sudo", $"chmod +x {installerPath}")
          .WaitForExitAsync(cancellationToken);

        // Use systemd-run to launch installer in a separate scope to prevent
        // it from being killed when the agent service stops.
        var systemdRunCommand = $"--scope {installerPath} {installCommand}";
        await _processManager.StartAndWaitForExit(
          "sudo",
          $"systemd-run {systemdRunCommand}",
          false,
          cancellationToken);
        break;

      case SystemPlatform.MacOs:
        await _processManager
          .Start("sudo", $"chmod +x {installerPath}")
          .WaitForExitAsync(cancellationToken);

        var launchdJobLabel = GetMacInstallerDaemonJobLabel(_instanceOptions.Value.InstanceId);
        var plistPath = GetMacInstallerDaemonPlistPath(_instanceOptions.Value.InstanceId);

        // The plist was rendered by the install already on disk, so its arguments are frozen and this
        // launch would otherwise carry none of the decisions made above. A migration depends on that,
        // since the brand being replaced is how the installer knows to retire this install. Launching
        // from an unmodified plist would install the new brand beside this one with both signing as
        // the same device, so decline instead.
        if (installArguments.Contains("--previous-brand-name", StringComparer.Ordinal)
          && !WriteMacInstallerDaemonArguments(plistPath, [installerPath, .. installArguments]))
        {
          return;
        }

        try
        {
          var bootoutStartInfo = GetMacLaunchctlStartInfo("launchctl", "bootout", $"system/{launchdJobLabel}");
          await _processManager.StartAndWaitForExit(bootoutStartInfo, TimeSpan.FromSeconds(15));
        }
        catch
        {
          // The updater job may not already be loaded.
        }

        var bootstrapStartInfo = GetMacLaunchctlStartInfo("launchctl", "bootstrap", "system", plistPath);
        await _processManager.StartAndWaitForExit(bootstrapStartInfo, TimeSpan.FromSeconds(15));

        var kickstartStartInfo = GetMacLaunchctlStartInfo("launchctl", "kickstart", "-k", $"system/{launchdJobLabel}");
        await _processManager.StartAndWaitForExit(kickstartStartInfo, TimeSpan.FromSeconds(15));
        break;

      default:
        throw new PlatformNotSupportedException();
    }
  }

  /// <summary>
  /// Rewrites the installer LaunchDaemon's <c>ProgramArguments</c> so the installer launches with the
  /// arguments this check decided on. The plist on disk was rendered by the install that is already in
  /// place, so without this, anything decided now cannot reach the installer on macOS. The whole array
  /// is replaced rather than appended to, which also refreshes server URI and tenant values that were
  /// frozen at install time.
  /// </summary>
  private bool WriteMacInstallerDaemonArguments(string plistPath, IReadOnlyList<string> programArguments)
  {
    if (!_fileSystem.FileExists(plistPath))
    {
      _logger.LogCritical(
        "Cannot migrate: the installer LaunchDaemon was not found at {PlistPath}, so the installer cannot be " +
        "told which brand it replaces. Nothing on this machine was changed.",
        plistPath);
      return false;
    }

    var plistContent = _fileSystem.ReadAllText(plistPath);
    var argumentsXml = string.Concat(programArguments.Select(
      argument => $"\n        <string>{SecurityElement.Escape(argument)}</string>"));

    // "$" in a replacement string is a group reference, so an argument containing one would corrupt the
    // rewrite. Doubling it escapes it, while the "$1" and "$2" group references below stay intact.
    var escapedArgumentsXml = argumentsXml.Replace("$", "$$");

    var updatedContent = Regex.Replace(
      plistContent,
      @"(<key>ProgramArguments</key>\s*<array>).*?(</array>)",
      $"$1{escapedArgumentsXml}\n    $2",
      RegexOptions.Singleline);

    if (string.Equals(updatedContent, plistContent, StringComparison.Ordinal))
    {
      _logger.LogCritical(
        "Cannot migrate: could not locate ProgramArguments in the installer LaunchDaemon at {PlistPath}, so the " +
        "installer cannot be told which brand it replaces. Nothing on this machine was changed.",
        plistPath);
      return false;
    }

    _fileSystem.WriteAllText(plistPath, updatedContent);
    _logger.LogInformation("Rewrote installer LaunchDaemon arguments at {PlistPath}.", plistPath);
    return true;
  }

  /// <summary>
  /// Copies this install's settings file into <paramref name="newBrandName"/>'s settings directory so
  /// the new brand's installer binds the existing device ID, tenant, and signing key instead of
  /// minting a new identity. The server only trusts the public key it already stored for a known
  /// device, so losing the key would leave the endpoint unable to authenticate again.
  /// </summary>
  private bool WriteSettingsFileForBrand(string newBrandName)
  {
    try
    {
      var sourcePath = _fileSystemPathProvider.GetAgentAppSettingsPath();
      if (!_fileSystem.FileExists(sourcePath))
      {
        _logger.LogError("Cannot migrate: this install's settings file was not found at {SourcePath}.", sourcePath);
        return false;
      }

      if (string.IsNullOrWhiteSpace(_optionsAccessor.PrivateKey))
      {
        _logger.LogError(
          "Cannot migrate: this install has no signing key, so the new brand's install could not authenticate as this device.");
        return false;
      }

      var destinationDirectory = _fileSystemPathProvider.GetSettingsDirectoryFor(newBrandName, _instanceOptions.Value.InstanceId);
      var destinationPath = Path.Combine(destinationDirectory, "appsettings.json");

      _fileSystem.CreateDirectory(destinationDirectory);
      _fileSystem.CopyFile(sourcePath, destinationPath, overwrite: true);

      // This file holds the device signing key, so it gets the same restrictions as this install's
      // own settings file rather than whatever the copy inherited.
      _optionsAccessor.RestrictAccess(destinationPath);

      _logger.LogInformation(
        "Wrote this install's settings to {DestinationPath} for the {NewBrandName} install to pick up.",
        destinationPath,
        newBrandName);

      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to hand the settings file to brand {NewBrandName}.", newBrandName);
      return false;
    }
  }
}