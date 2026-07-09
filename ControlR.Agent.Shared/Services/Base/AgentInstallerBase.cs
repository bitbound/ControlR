using ControlR.Agent.Shared.Options;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
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

  protected static string GetAgentPath(string installDirectory, SystemPlatform platform)
  {
    return Path.Combine(installDirectory, AppConstants.GetAgentFileName(platform));
  }

  protected static string GetInstanceInstallDirectory(string rootDirectory, string? instanceId)
  {
    var installDirectoryName = string.IsNullOrWhiteSpace(instanceId)
      ? AppConstants.DefaultInstallDirectoryName
      : instanceId;

    return Path.Combine(rootDirectory, installDirectoryName);
  }

  protected async Task<Result> CreateDeviceOnServer(Guid? installerKeyId, string? installerKeySecret, Guid[]? tagIds)
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
      deviceDto, installerKeyId.Value, installerKeySecret, tagIds, publicKeyBase64);

    if (tagIds is null)
    {
      Logger.LogInformation("Requesting device creation on the server with no tags.");
    }
    else
    {
      Logger.LogInformation("Requesting device creation on the server with tags {TagIds}.", string.Join(", ", tagIds));
    }

    var createResult = await _controlrApi.Internal.Devices.CreateDevice(createRequest);
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

}
