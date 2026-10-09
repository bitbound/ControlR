using ControlR.Agent.Shared.Options;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using ControlR.Libraries.Branding;
using ControlR.Libraries.Shared.Services.Encryption;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Shared.Services.Base;

internal abstract class AgentInstallerBase(
  IFileSystem fileSystem,
  IFileSystemPathProvider fileSystemPathProvider,
  IControlrApi controlrApi,
  IDeviceInfoProvider deviceDataGenerator,
  IOptionsAccessor optionsAccessor,
  IProcessManager processManager,
  ISystemEnvironment systemEnvironment,
  IOptionsMonitor<AgentAppOptions> appOptions,
  ILogger<AgentInstallerBase> logger,
  IEd25519KeyProvider keyProvider)
{
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly IDeviceInfoProvider _deviceDataGenerator = deviceDataGenerator;
  private readonly IEd25519KeyProvider _keyProvider = keyProvider;
  private readonly IOptionsAccessor _optionsAccessor = optionsAccessor;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;

  protected IOptionsMonitor<AgentAppOptions> AppOptions { get; } = appOptions;
  protected IFileSystem FileSystem { get; } = fileSystem;
  protected IFileSystemPathProvider FilesystemPathProvider { get; } = fileSystemPathProvider;
  protected ILogger<AgentInstallerBase> Logger { get; } = logger;
  protected IProcessManager ProcessManager { get; } = processManager;

  /// <summary>
  /// Starts the service of the install being replaced. Used to roll back when this install cannot
  /// start, so a failed migration does not leave the machine with no running agent.
  /// </summary>
  public async Task<Result> RestorePreviousBrand(string previousBrandName, string? previousInstanceId)
  {
    using var _ = Logger.BeginMemberScope();

    Logger.LogInformation(
      "Starting the {PreviousBrandName} service again. Instance id: {PreviousInstanceId}",
      previousBrandName,
      previousInstanceId);

    var result = await RunPreviousBrandAgentCommand(
      previousBrandName,
      previousInstanceId,
      "start-service",
      TimeSpan.FromMinutes(2));

    if (result.IsSuccess)
    {
      Logger.LogInformation("The {PreviousBrandName} service is running again.", previousBrandName);
    }
    else
    {
      Logger.LogError(
        "Failed to start the {PreviousBrandName} service again: {Reason}. This machine may be left with no running agent.",
        previousBrandName,
        result.Reason);
    }

    return result;
  }

  /// <summary>
  /// Removes the install being replaced, and the settings directory its uninstall leaves behind.
  /// </summary>
  public async Task<Result> RetirePreviousBrand(string previousBrandName, string? previousInstanceId)
  {
    using var _ = Logger.BeginMemberScope();

    Logger.LogInformation(
      "Removing the {PreviousBrandName} install. Instance id: {PreviousInstanceId}",
      previousBrandName,
      previousInstanceId);

    var result = await RunPreviousBrandAgentCommand(
      previousBrandName,
      previousInstanceId,
      "uninstall",
      TimeSpan.FromMinutes(10));

    if (!result.IsSuccess)
    {
      Logger.LogError(
        "Failed to remove the {PreviousBrandName} install: {Reason}. This install is running; the old service and its files need manual removal.",
        previousBrandName,
        result.Reason);
      return result;
    }

    // Uninstall deliberately keeps the settings directory, and that file holds the device signing key
    // in plaintext. Remove it so the retired brand does not keep holding it.
    try
    {
      var settingsDirectory = FilesystemPathProvider.GetSettingsDirectoryFor(previousBrandName, previousInstanceId);

      if (FileSystem.DirectoryExists(settingsDirectory))
      {
        FileSystem.DeleteDirectory(settingsDirectory, true);
      }
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "Failed to delete the retired install's settings directory.");
    }

    Logger.LogInformation("Removed the {PreviousBrandName} install.", previousBrandName);
    return Result.Ok();
  }

  protected static string GetAgentPath(string installDirectory, SystemPlatform platform, string brandName)
  {
    // The executable is named for its own brand, so addressing an install of another brand means using
    // that brand's file name rather than this build's.
    return Path.Combine(installDirectory, AppConstants.GetAgentFileName(platform, brandName));
  }

  protected static string GetInstanceInstallDirectory(string rootDirectory, string? instanceId)
  {
    var installDirectoryName = string.IsNullOrWhiteSpace(instanceId)
      ? AppConstants.DefaultInstanceId
      : instanceId;

    return Path.Combine(rootDirectory, installDirectoryName);
  }

  protected async Task<Result> CreateDeviceOnServer(Guid? installerKeyId, string? installerKeySecret, Guid[]? tagIds, Guid? customerId)
  {
    if (installerKeyId is null)
    {
      return Result.Ok();
    }

    if (string.IsNullOrWhiteSpace(installerKeySecret))
    {
      return Result.Fail("Installer key secret is required when installer key ID is provided.");
    }

    var (publicKey, privateKey) = _keyProvider.GenerateKeyPair();
    var privateKeyBase64 = Convert.ToBase64String(privateKey);
    var publicKeyBase64 = Convert.ToBase64String(publicKey);

    AppOptions.CurrentValue.PrivateKey = privateKeyBase64;
    await _optionsAccessor.UpdateAppOptions(AppOptions.CurrentValue);

    var deviceDto = await _deviceDataGenerator.GetDeviceInfo();
    var createRequest = new CreateDeviceRequestDto(
      deviceDto, installerKeyId.Value, installerKeySecret, tagIds, publicKeyBase64, customerId);

    if (tagIds is null)
    {
      Logger.LogInformation("Requesting device creation on the server with no tags.");
    }
    else
    {
      Logger.LogInformation("Requesting device creation on the server with tags {TagIds}.", string.Join(", ", tagIds));
    }

    var createResult = await _controlrApi.Agent.Devices.CreateDevice(createRequest);
    if (createResult.IsSuccess)
    {
      Logger.LogInformation("Device created successfully.");
    }
    else
    {
      Logger.LogError("Device creation failed.  Reason: {Reason}", createResult.Reason);
    }

    return createResult.ToResult();
  }

  protected async Task ExtractBundleToInstallDirectory(
    string bundleZipPath,
    string installDirectory,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(bundleZipPath))
    {
      throw new ArgumentException("Bundle zip path is required.", nameof(bundleZipPath));
    }

    if (!FileSystem.FileExists(bundleZipPath))
    {
      throw new FileNotFoundException($"Bundle zip '{bundleZipPath}' does not exist.", bundleZipPath);
    }

    FileSystem.CreateDirectory(installDirectory);
    await FileSystem.ExtractZipArchiveAsync(bundleZipPath, installDirectory, overwriteFiles: true, cancellationToken);
  }

  /// <summary>
  /// Stops the service of the install being replaced, before this install starts its own. Only one
  /// agent may hold the device's connection on the server, and both would otherwise keep taking it, so
  /// the caller has to be able to refuse to continue when this fails.
  /// </summary>
  protected async Task<Result> StopPreviousBrandService(string? previousBrandName, string? previousInstanceId)
  {
    if (string.IsNullOrWhiteSpace(previousBrandName))
    {
      return Result.Ok();
    }

    using var _ = Logger.BeginMemberScope();
    Logger.LogInformation(
      "Stopping the {PreviousBrandName} service before starting this one. Instance id: {PreviousInstanceId}",
      previousBrandName,
      previousInstanceId);

    var result = await RunPreviousBrandAgentCommand(
      previousBrandName,
      previousInstanceId,
      "stop-service",
      TimeSpan.FromMinutes(2));

    if (!result.IsSuccess)
    {
      Logger.LogError(
        "Could not stop the {PreviousBrandName} service: {Reason}. Starting this install anyway would leave both signing as the same device.",
        previousBrandName,
        result.Reason);
    }

    return result;
  }

  protected Result StopProcesses(string targetAgentPath, string? targetDesktopClientPath = null)
  {
    try
    {
      var comparison = _systemEnvironment.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

      var procs = ProcessManager
        .GetProcessesByName(BrandingConstants.AgentBaseName)
        .Where(x =>
          x.Id != _systemEnvironment.ProcessId &&
          string.Equals(x.FilePath, targetAgentPath, comparison));

      foreach (var proc in procs)
      {
        try
        {
          proc.Kill();
        }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Failed to kill agent process with ID {AgentProcessId}.", proc.Id);
        }
      }

      procs = ProcessManager
        .GetProcessesByName(BrandingConstants.DesktopClientBaseName)
        .Where(x =>
          targetDesktopClientPath is not null &&
          string.Equals(x.FilePath, targetDesktopClientPath, comparison));

      foreach (var proc in procs)
      {
        try
        {
          proc.Kill();
        }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Failed to kill desktop client process with ID {DesktopClientProcessId}.", proc.Id);
        }
      }

      return Result.Ok();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while stopping service and processes.");
      return Result.Fail(ex);
    }
  }


  protected async Task UpdateAppSettings(Uri? serverUri, Guid? tenantId, Guid? deviceId)
  {
    using var _ = Logger.BeginMemberScope();
    var currentOptions = AppOptions.CurrentValue;

    var updatedServerUri =
      BrandingConstants.ControlrServerUrl ??
      serverUri ??
      currentOptions.ServerUri ??
      AppConstants.ServerUri;

    var updatedTenantId =
      tenantId ??
      currentOptions.TenantId;

    var updatedDeviceId =
      deviceId ??
      currentOptions.DeviceId;

    Logger.LogInformation("Setting server URI to {ServerUri}.", updatedServerUri);
    currentOptions.ServerUri = updatedServerUri;

    Logger.LogInformation("Setting tenant ID to {TenantId}.", updatedTenantId);
    currentOptions.TenantId = updatedTenantId;

    if (updatedDeviceId == Guid.Empty)
    {
      Logger.LogInformation("DeviceId is empty.  Generating new one.");
      currentOptions.DeviceId = Guid.NewGuid();
    }
    else
    {
      Logger.LogInformation("Setting device ID to {DeviceId}.", updatedDeviceId);
      currentOptions.DeviceId = updatedDeviceId;
    }

    Logger.LogInformation("Writing results to disk.");
    await _optionsAccessor.UpdateAppOptions(currentOptions);
  }

  protected async Task WriteBundleHashFile(string? bundleSha256)
  {
    if (string.IsNullOrWhiteSpace(bundleSha256))
    {
      return;
    }

    var bundleHashPath = FilesystemPathProvider.GetBundleHashFilePath();
    var settingsDirectory = Path.GetDirectoryName(bundleHashPath)
      ?? throw new DirectoryNotFoundException("Unable to determine the bundle hash directory.");

    Logger.LogInformation("Writing bundle hash to {BundleHashPath}.", bundleHashPath);
    FileSystem.CreateDirectory(settingsDirectory);
    await FileSystem.WriteAllTextAsync(bundleHashPath, bundleSha256.Trim());
  }

  /// <summary>
  /// Runs one of the replaced install's own commands against itself. The install is addressed by the
  /// brand and instance id it was created with, which is not always this install's, because a
  /// migration can move either. Staged to a temp copy first, because run from its own install
  /// directory that agent copies itself elsewhere, re-launches detached, and returns before doing
  /// anything. Asking it to act on itself is also what keeps another brand's service names, unit
  /// files, and registry keys out of this code.
  /// </summary>
  private async Task<Result> RunPreviousBrandAgentCommand(
    string previousBrandName,
    string? previousInstanceId,
    string command,
    TimeSpan timeout)
  {
    var installDirectory = FilesystemPathProvider.GetAgentInstallDirectoryFor(previousBrandName, previousInstanceId);
    var agentPath = GetAgentPath(installDirectory, _systemEnvironment.Platform, previousBrandName);

    if (!FileSystem.FileExists(agentPath))
    {
      return Result.Fail($"The {previousBrandName} agent was not found at {agentPath}.");
    }

    var stagedPath = Path.Combine(
      Path.GetTempPath(),
      $"{BrandingConstants.SanitizeBrandKey(previousBrandName)}_{Guid.NewGuid():N}{Path.GetExtension(agentPath)}");

    FileSystem.CopyFile(agentPath, stagedPath, true);

    try
    {
      var arguments = command;
      if (!string.IsNullOrWhiteSpace(previousInstanceId))
      {
        // The replaced install's own service and paths are keyed on the instance id it was created
        // with, so that install's commands have to be told the same value.
        arguments += $" \"--instance-id\" \"{previousInstanceId}\"";
      }

      var exitCode = await ProcessManager.StartAndWaitForExit(stagedPath, arguments, false, timeout);

      return exitCode == 0
        ? Result.Ok()
        : Result.Fail($"'{command}' on the {previousBrandName} install exited with code {exitCode}.");
    }
    catch (Exception ex)
    {
      return Result.Fail(ex);
    }
    finally
    {
      try
      {
        if (FileSystem.FileExists(stagedPath))
        {
          FileSystem.DeleteFile(stagedPath);
        }
      }
      catch (Exception ex)
      {
        Logger.LogWarning(ex, "Failed to delete staged agent copy {StagedPath}.", stagedPath);
      }
    }
  }

}
