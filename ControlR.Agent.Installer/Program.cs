using ControlR.Agent.Installer.Services;
using ControlR.Agent.Shared.Interfaces;
using ControlR.Agent.Shared.Models;
using ControlR.Agent.Shared.Options;
using ControlR.Agent.Shared.Services;
using ControlR.Agent.Shared.Services.Linux;
using ControlR.Agent.Shared.Services.Mac;
using ControlR.Agent.Shared.Services.Windows;
using ControlR.Agent.Shared.Startup;
using ControlR.ApiClient;
using ControlR.Libraries.Branding;
using ControlR.Libraries.Serilog;
using ControlR.Libraries.Shared.DataValidation;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Parsing;

const string RootDescription = "ControlR agent installer.";
const string InstallCommandName = "install";
const string RepairDesktopCommandName = "repair-desktop";
const string UninstallCommandName = "uninstall";
const string InstallCommandDescription = "Install the ControlR agent bundle.";
const string RepairDesktopCommandDescription = "Repair the installed desktop client payload without modifying the agent service.";
const string UninstallCommandDescription = "Uninstall the ControlR agent bundle.";
const string ServerUriDescription = "The fully-qualified server URI to which the agent will connect (e.g. 'https://my.example.com' or 'https://my.example.com:8080').";
const string InstanceIdDescription = "An instance ID for this agent installation, which allows multiple agent installations.  This is typically the server origin (e.g. 'example.controlr.app').";
const string DeviceTagsDescription = "An optional, comma-separated list of tags to which the agent will be assigned.";
const string TenantIdDescription = "The tenant ID to which the agent will be assigned.";
const string InstallerKeySecretDescription = "An access key that will allow the device to be created on the server.";
const string InstallerKeyIdDescription = "The ID of the installer key to use for installation.";
const string DeviceIdDescription = "An optional device ID to which the agent will be assigned.  If omitted, the installer will either use the existing device ID saved on the system (if present) or create a new, random ID.";
const string ServerUriShortAlias = "-s";
const string ServerUriLongAlias = "--server-uri";
const string InstanceIdShortAlias = "-i";
const string InstanceIdLongAlias = "--instance-id";
const string DeviceTagsShortAlias = "-g";
const string DeviceTagsLongAlias = "--device-tags";
const string TenantIdShortAlias = "-t";
const string TenantIdLongAlias = "--tenant-id";
const string InstallerKeySecretShortAlias = "-ks";
const string InstallerKeySecretLongAlias = "--installer-key-secret";
const string InstallerKeyIdShortAlias = "-ki";
const string InstallerKeyIdLongAlias = "--installer-key-id";
const string DeviceIdShortAlias = "-d";
const string DeviceIdLongAlias = "--device-id";
const string CustomerIdShortAlias = "-c";
const string CustomerIdLongAlias = "--customer";
const string CustomerIdDescription = "An optional customer ID to assign the device to upon installation.";
const string PreviousBrandNameDescription = "Brand name of the agent install being replaced. Set only during a cross-brand migration. The server must declare this name as a predecessor of the installer's own brand, or the install is refused. When set, the previous install's settings file is adopted so the device ID and signing key survive the rebrand.";
const string PreviousAgentPathDescription = "Full path to the executable of the agent install being replaced. Used to retire that install once the new brand is installed successfully.";
const string PreserveMachinePolicyDescription = "Leave machine-wide policy values untouched. Used when another agent install still depends on them, which is the case when retiring an install that a new brand has already replaced.";
const string PreviousBrandNameLongAlias = "--previous-brand-name";
const string PreviousAgentPathLongAlias = "--previous-agent-path";
const string PreserveMachinePolicyLongAlias = "--preserve-machine-policy";
const string TempDirectoryPrefix = "controlr-install-";
const string TempBundleFileName = "ControlR.Agent.bundle.zip";

var rootCommand = new RootCommand(RootDescription)
{
  GetInstallCommand(),
  GetRepairDesktopCommand(),
  GetUninstallCommand(),
};

return await rootCommand.Parse(args).InvokeAsync();

static Command GetInstallCommand()
{
  var serverUriOption = new Option<Uri>(ServerUriShortAlias, ServerUriLongAlias)
  {
    Required = true,
    Description = ServerUriDescription,
    CustomParser = result =>
    {
      if (result.Tokens.Count == 0)
      {
        result.AddError("Server URI is required.");
        return null!;
      }

      var uriArg = result.Tokens[0].Value;
      if (Uri.TryCreate(uriArg, UriKind.Absolute, out var uri))
      {
        return uri;
      }

      result.AddError(
        $"The server URI '{uriArg}' is not a valid absolute URI. " +
        "Please provide a valid URI including the scheme (e.g. 'https://').");

      return null!;
    }
  };

  var instanceIdOption = new Option<string?>(InstanceIdShortAlias, InstanceIdLongAlias)
  {
    Description = InstanceIdDescription,
  };

  instanceIdOption.Validators.Add(ValidateInstanceId);

  var deviceTagsOption = new Option<string?>(DeviceTagsShortAlias, DeviceTagsLongAlias)
  {
    Description = DeviceTagsDescription
  };

  var tenantIdOption = new Option<Guid?>(TenantIdShortAlias, TenantIdLongAlias)
  {
    Required = true,
    Description = TenantIdDescription
  };

  var installerKeySecretOption = new Option<string?>(InstallerKeySecretShortAlias, InstallerKeySecretLongAlias)
  {
    Description = InstallerKeySecretDescription
  };

  var installerKeyIdOption = new Option<Guid?>(InstallerKeyIdShortAlias, InstallerKeyIdLongAlias)
  {
    Description = InstallerKeyIdDescription
  };

  var deviceIdOption = new Option<Guid?>(DeviceIdShortAlias, DeviceIdLongAlias)
  {
    Required = false,
    Description = DeviceIdDescription
  };

  var customerIdOption = new Option<Guid?>(CustomerIdShortAlias, CustomerIdLongAlias)
  {
    Required = false,
    Description = CustomerIdDescription
  };

  var previousBrandNameOption = new Option<string?>(PreviousBrandNameLongAlias)
  {
    Required = false,
    Description = PreviousBrandNameDescription,
  };

  var previousAgentPathOption = new Option<string?>(PreviousAgentPathLongAlias)
  {
    Required = false,
    Description = PreviousAgentPathDescription,
  };

  var installCommand = new Command(InstallCommandName, InstallCommandDescription)
  {
    serverUriOption,
    instanceIdOption,
    deviceTagsOption,
    tenantIdOption,
    installerKeySecretOption,
    installerKeyIdOption,
    deviceIdOption,
    customerIdOption,
    previousBrandNameOption,
    previousAgentPathOption,
  };

  installCommand.SetAction(async parseResult =>
  {
    var installRequest = new AgentInstallRequest
    {
      BundleZipPath = string.Empty,
      ServerUri = parseResult.GetRequiredValue(serverUriOption),
      TenantId = parseResult.GetRequiredValue(tenantIdOption)!.Value,
      InstallerKeySecret = parseResult.GetValue(installerKeySecretOption),
      InstallerKeyId = parseResult.GetValue(installerKeyIdOption),
      DeviceId = parseResult.GetValue(deviceIdOption),
      TagIds = ParseTagIds(parseResult.GetValue(deviceTagsOption)),
      CustomerId = parseResult.GetValue(customerIdOption),
    };

    return await RunInstall(
      installRequest,
      parseResult.GetValue(instanceIdOption),
      parseResult.GetValue(previousBrandNameOption),
      parseResult.GetValue(previousAgentPathOption));
  });

  return installCommand;
}

static Command GetUninstallCommand()
{
  var instanceIdOption = new Option<string?>(InstanceIdShortAlias, InstanceIdLongAlias)
  {
    Description = InstanceIdDescription
  };

  instanceIdOption.Validators.Add(ValidateInstanceId);

  var preserveMachinePolicyOption = new Option<bool>(PreserveMachinePolicyLongAlias)
  {
    Description = PreserveMachinePolicyDescription,
  };

  var uninstallCommand = new Command(UninstallCommandName, UninstallCommandDescription)
  {
    instanceIdOption,
    preserveMachinePolicyOption,
  };

  uninstallCommand.SetAction(async parseResult => await RunUninstall(
    parseResult.GetValue(instanceIdOption),
    parseResult.GetValue(preserveMachinePolicyOption)));

  return uninstallCommand;
}

static Command GetRepairDesktopCommand()
{
  var instanceIdOption = new Option<string?>(InstanceIdShortAlias, InstanceIdLongAlias)
  {
    Description = InstanceIdDescription
  };

  instanceIdOption.Validators.Add(ValidateInstanceId);

  var repairCommand = new Command(RepairDesktopCommandName, RepairDesktopCommandDescription)
  {
    instanceIdOption,
  };

  repairCommand.SetAction(async parseResult => await RunRepairDesktop(parseResult.GetValue(instanceIdOption)));

  return repairCommand;
}

static async Task<int> RunInstall(
  AgentInstallRequest request,
  string? instanceId,
  string? previousBrandName,
  string? previousAgentPath)
{
  // Adopted before the host is built so that the normal configuration load picks the inherited
  // values up and AgentAppOptions binds the previous install's device ID and signing key.
  var (adoptedSettingsPath, adoptionNote) = AdoptPreviousBrandSettings(previousBrandName, instanceId);

  using var host = CreateInstallerHost(instanceId, request.ServerUri);
  var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ControlR.Agent.Installer");
  using var logScope = logger.BeginScope("RunInstall. InstanceId: {InstanceId}", instanceId);
  var fileSystem = host.Services.GetRequiredService<IFileSystem>();
  var tempDir = Path.Combine(Path.GetTempPath(), $"{TempDirectoryPrefix}{Guid.NewGuid():N}");
  var tempBundlePath = Path.Combine(tempDir, TempBundleFileName);

  try
  {
    var api = host.Services.GetRequiredService<IControlrApi>();
    var downloader = host.Services.GetRequiredService<IBundleDownloader>();
    var installer = host.Services.GetRequiredService<IAgentInstaller>();
    var systemEnvironment = host.Services.GetRequiredService<ISystemEnvironment>();

    logger.LogInformation("ControlR Agent Installer started.");
    logger.LogInformation("Server URI: {ServerUri}", request.ServerUri);

    var runtime = systemEnvironment.Runtime;
    logger.LogInformation("Detected runtime: {Runtime}", runtime);

    var metadataResult = await api.Agent.Updates.GetBundleMetadata(runtime);
    if (!metadataResult.IsSuccess || metadataResult.Value is null)
    {
      logger.LogError("Failed to fetch bundle metadata. Reason: {Reason}", metadataResult.Reason);
      DiscardAdoptedSettings(fileSystem, logger, adoptedSettingsPath);
      return 1;
    }

    var metadata = metadataResult.Value;
    logger.LogInformation("Bundle version: {Version}", metadata.Version);

    var brandMatch = BrandCompatibility.Evaluate(
      metadata.BrandName,
      metadata.Publisher,
      metadata.PredecessorBrandNames);

    if (brandMatch == BrandMatch.SameBrand && adoptedSettingsPath is not null)
    {
      logger.LogWarning(
        "A previous brand name was supplied but this bundle is the installer's own brand. " +
        "Discarding the adopted settings and continuing as an ordinary install.");
      DiscardAdoptedSettings(fileSystem, logger, adoptedSettingsPath);
      adoptedSettingsPath = null;
    }

    if (brandMatch == BrandMatch.DeclaredPredecessor && !IsDeclaredPredecessor(metadata.PredecessorBrandNames, previousBrandName))
    {
      logger.LogCritical(
        "Refusing to migrate: this server declares brand(s) {DeclaredPredecessors} as predecessors, which does not include '{PreviousBrandName}'. " +
        "Installer brand: {InstallerBrandName}. Nothing on this machine was changed.",
        string.Join(", ", metadata.PredecessorBrandNames ?? []),
        previousBrandName,
        BrandingConstants.BrandName);
      DiscardAdoptedSettings(fileSystem, logger, adoptedSettingsPath);
      return 1;
    }

    if (brandMatch != BrandMatch.SameBrand && brandMatch != BrandMatch.DeclaredPredecessor)
    {
      logger.LogCritical(
        "Refusing to install: server bundle is for a different brand than this installer, and neither declares the other as a predecessor. " +
        "Installer brand: {InstallerBrandName}/{InstallerPublisher}, Server brand: {ServerBrandName}/{ServerPublisher}. " +
        "If this server is correct, run the matching installer instead.",
        BrandingConstants.BrandName,
        BrandingConstants.Publisher,
        metadata.BrandName,
        metadata.Publisher);
      DiscardAdoptedSettings(fileSystem, logger, adoptedSettingsPath);
      return 1;
    }

    var isBrandMigration = brandMatch == BrandMatch.DeclaredPredecessor;

    if (isBrandMigration)
    {
      // Without the previous install's settings file there is no device ID and no signing key to carry
      // over. Proceeding would register the endpoint as a new device and sign it with a key the server
      // has never been told about, which is unrecoverable without an operator re-enrolling it.
      if (adoptedSettingsPath is null)
      {
        logger.LogCritical(
          "Refusing to migrate from brand {PreviousBrandName}: {Note}. " +
          "Migrating without the previous install's settings would lose its device ID and signing key. " +
          "Nothing on this machine was changed.",
          previousBrandName,
          adoptionNote);
        DiscardAdoptedSettings(fileSystem, logger, adoptedSettingsPath);
        return 1;
      }

      logger.LogWarning(
        "Migrating from brand {PreviousBrandName} to {InstallerBrandName}. Device identity and signing key carry over from the previous install.",
        previousBrandName,
        BrandingConstants.BrandName);
    }
    else if (adoptionNote is not null)
    {
      logger.LogWarning("Adopting previous brand settings did not happen. {Note}", adoptionNote);
    }

    logger.LogInformation("Downloading bundle to temp file: {TempBundlePath}", tempBundlePath);
    await downloader.DownloadBundle(metadata.BundleDownloadUrl, metadata.BundleSha256, tempBundlePath);

    var installRequest = new AgentInstallRequest
    {
      BundleSha256 = metadata.BundleSha256,
      BundleZipPath = tempBundlePath,
      ServerUri = request.ServerUri,
      TenantId = request.TenantId,
      InstallerKeySecret = request.InstallerKeySecret,
      InstallerKeyId = request.InstallerKeyId,
      DeviceId = request.DeviceId,
      TagIds = request.TagIds,
      CustomerId = request.CustomerId,
    };

    await installer.Install(installRequest);

    logger.LogInformation("Installation completed successfully.");

    if (isBrandMigration)
    {
      // Best effort. The new install is already working, so a failure here must not report a failed
      // install. A leftover old-brand service is recoverable; reporting success as failure is not.
      await RetirePreviousInstallAsync(host.Services, logger, fileSystem, previousBrandName, previousAgentPath, instanceId);
    }

    return 0;
  }
  catch (Exception ex)
  {
    logger.LogError(ex, "Installation failed.");
    return 1;
  }
  finally
  {
    if (fileSystem.DirectoryExists(tempDir))
    {
      try
      {
        fileSystem.DeleteDirectory(tempDir, true);
      }
      catch (Exception ex)
      {
        logger.LogWarning(ex, "Failed to delete temporary directory {TempDir}.", tempDir);
      }
    }
  }
}

static async Task<int> RunUninstall(string? instanceId, bool preserveMachinePolicy)
{
  using var host = CreateInstallerHost(instanceId, serverUri: null);
  var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ControlR.Agent.Installer");
  var installer = host.Services.GetRequiredService<IAgentInstaller>();

  try
  {
    await installer.Uninstall(preserveMachinePolicy);
    logger.LogInformation("Uninstall completed successfully.");
    return 0;
  }
  catch (Exception ex)
  {
    logger.LogError(ex, "Uninstall failed.");
    return 1;
  }
}

static async Task<int> RunRepairDesktop(string? instanceId)
{
  using var host = CreateInstallerHost(instanceId, serverUri: null);
  var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ControlR.Agent.Installer");
  using var logScope = logger.BeginScope("RunRepairDesktop. InstanceId: {InstanceId}", instanceId);
  var fileSystem = host.Services.GetRequiredService<IFileSystem>();
  var tempDir = Path.Combine(Path.GetTempPath(), $"{TempDirectoryPrefix}{Guid.NewGuid():N}");
  var tempBundlePath = Path.Combine(tempDir, TempBundleFileName);

  try
  {
    var api = host.Services.GetRequiredService<IControlrApi>();
    var downloader = host.Services.GetRequiredService<IBundleDownloader>();
    var installer = host.Services.GetRequiredService<IAgentInstaller>();
    var systemEnvironment = host.Services.GetRequiredService<ISystemEnvironment>();
    var optionsAccessor = host.Services.GetRequiredService<IOptionsAccessor>();

    logger.LogInformation("ControlR desktop repair started.");

    var runtime = systemEnvironment.Runtime;
    logger.LogInformation("Detected runtime: {Runtime}", runtime);

    var metadataResult = await api.Agent.Updates.GetBundleMetadata(runtime);
    if (!metadataResult.IsSuccess || metadataResult.Value is null)
    {
      logger.LogError("Failed to fetch bundle metadata. Reason: {Reason}", metadataResult.Reason);
      return 1;
    }

    var metadata = metadataResult.Value;
    logger.LogInformation("Bundle version: {Version}", metadata.Version);

    // Repair writes the desktop client payload into an existing install, so it only ever applies to
    // that install's own brand. A cross-brand bundle is served by the install command, which moves
    // the whole install rather than dropping a foreign payload into the wrong directory.
    var brandMatch = BrandCompatibility.Evaluate(
      metadata.BrandName,
      metadata.Publisher,
      metadata.PredecessorBrandNames);

    if (brandMatch != BrandMatch.SameBrand)
    {
      logger.LogCritical(
        "Refusing to repair desktop client: server bundle is not this installer's brand. Match result: {BrandMatch}. " +
        "Installer brand: {InstallerBrandName}/{InstallerPublisher}, Server brand: {ServerBrandName}/{ServerPublisher}. " +
        "If this server is correct, run the matching installer instead.",
        brandMatch,
        BrandingConstants.BrandName,
        BrandingConstants.Publisher,
        metadata.BrandName,
        metadata.Publisher);
      return 1;
    }

    logger.LogInformation("Downloading bundle to temp file: {TempBundlePath}", tempBundlePath);
    await downloader.DownloadBundle(metadata.BundleDownloadUrl, metadata.BundleSha256, tempBundlePath);

    var repairRequest = new AgentInstallRequest
    {
      BundleSha256 = metadata.BundleSha256,
      BundleZipPath = tempBundlePath,
      ServerUri = optionsAccessor.ServerUri,
      TenantId = optionsAccessor.GetRequiredTenantId(),
      DeviceId = optionsAccessor.DeviceId,
    };

    await installer.RepairDesktopClient(repairRequest);

    logger.LogInformation("Desktop repair completed successfully.");
    return 0;
  }
  catch (Exception ex)
  {
    logger.LogError(ex, "Desktop repair failed.");
    return 1;
  }
  finally
  {
    if (fileSystem.DirectoryExists(tempDir))
    {
      try
      {
        fileSystem.DeleteDirectory(tempDir, true);
      }
      catch (Exception ex)
      {
        logger.LogWarning(ex, "Failed to delete temporary directory {TempDir}.", tempDir);
      }
    }
  }
}

static IHost CreateInstallerHost(string? instanceId, Uri? serverUri)
{
  var builder = Host.CreateApplicationBuilder();
  builder.AddControlRInstallerServices(instanceId, serverUri, loadAppSettings: true);
  var pathProvider = GetTempPathProvider(builder);
  builder.BootstrapSerilog(pathProvider.GetInstallerLogFilePath(), TimeSpan.FromDays(7));
  builder.Services.AddSingleton<IBundleDownloader, BundleDownloader>();
  return builder.Build();
}

static FileSystemPathProvider GetTempPathProvider(HostApplicationBuilder builder)
{
  var instanceOptions = builder.Configuration
    .GetSection(InstanceOptions.SectionKey)
    .Get<InstanceOptions>() ?? new InstanceOptions();

  return CreateStandalonePathProvider(instanceOptions.InstanceId);
}

/// <summary>
/// Builds a path provider without a service container, for the steps that must run before the host
/// exists or that need to address a brand other than the one compiled into this executable.
/// </summary>
static FileSystemPathProvider CreateStandalonePathProvider(string? instanceId)
{
  IElevationChecker elevationChecker =
    SystemEnvironment.Instance.IsWindows()
      ? new ElevationCheckerWin()
      : SystemEnvironment.Instance.IsMacOS()
        ? new ElevationCheckerMac()
        : SystemEnvironment.Instance.IsLinux()
          ? new ElevationCheckerLinux()
          : throw new PlatformNotSupportedException();

  return new FileSystemPathProvider(
    SystemEnvironment.Instance,
    elevationChecker,
    new FileSystem(new SerilogLogger<FileSystem>()),
    new OptionsMonitorWrapper<InstanceOptions>(new InstanceOptions { InstanceId = instanceId }));
}

/// <summary>
/// Copies the settings file of the install being replaced onto this brand's settings path so the
/// ordinary configuration load binds its device ID, tenant, and signing key. Without that file the
/// new install would mint a fresh device ID and a fresh signing key, and the server only ever trusts
/// the public key it already stored for a known device, so the endpoint would be unable to
/// authenticate again. Returns the copied path plus a note explaining why nothing was copied.
/// </summary>
static (string? AdoptedPath, string? Note) AdoptPreviousBrandSettings(string? previousBrandName, string? instanceId)
{
  if (string.IsNullOrWhiteSpace(previousBrandName))
  {
    return (null, null);
  }

  try
  {
    var pathProvider = CreateStandalonePathProvider(instanceId);
    var sourcePath = Path.Combine(
      pathProvider.GetSettingsDirectoryFor(previousBrandName, instanceId),
      "appsettings.json");
    var destinationPath = pathProvider.GetAgentAppSettingsPath();

    if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
    {
      return (null, null);
    }

    var fileSystem = new FileSystem(new SerilogLogger<FileSystem>());
    if (!fileSystem.FileExists(sourcePath))
    {
      return (null, $"No settings file found for brand '{previousBrandName}' at {sourcePath}.");
    }

    var destinationDirectory = Path.GetDirectoryName(destinationPath)
      ?? throw new InvalidOperationException($"'{destinationPath}' has no parent directory.");

    fileSystem.CreateDirectory(destinationDirectory);
    fileSystem.CopyFile(sourcePath, destinationPath, true);
    return (destinationPath, null);
  }
  catch (Exception ex)
  {
    return (null, $"Failed to copy settings from brand '{previousBrandName}': {ex.Message}");
  }
}

static void DiscardAdoptedSettings(IFileSystem fileSystem, ILogger logger, string? adoptedSettingsPath)
{
  if (adoptedSettingsPath is null)
  {
    return;
  }

  try
  {
    if (fileSystem.FileExists(adoptedSettingsPath))
    {
      fileSystem.DeleteFile(adoptedSettingsPath);
    }
  }
  catch (Exception ex)
  {
    logger.LogWarning(ex, "Failed to discard adopted settings file {Path}.", adoptedSettingsPath);
  }
}

static bool IsDeclaredPredecessor(string[]? declaredPredecessorBrandNames, string? previousBrandName)
{
  if (string.IsNullOrWhiteSpace(previousBrandName))
  {
    return false;
  }

  if (declaredPredecessorBrandNames is null || declaredPredecessorBrandNames.Length == 0)
  {
    return false;
  }

  var previousKey = BrandingConstants.SanitizeBrandKey(previousBrandName);

  return declaredPredecessorBrandNames.Any(x =>
    !string.IsNullOrWhiteSpace(x) &&
    string.Equals(BrandingConstants.SanitizeBrandKey(x!), previousKey, StringComparison.Ordinal));
}

/// <summary>
/// Asks the replaced install to remove itself. Best effort by design: the new install is already
/// working at this point, and a leftover old-brand service is far cheaper to recover than an install
/// reported as failed after it succeeded.
/// </summary>
static async Task RetirePreviousInstallAsync(
  IServiceProvider services,
  ILogger logger,
  IFileSystem fileSystem,
  string? previousBrandName,
  string? previousAgentPath,
  string? instanceId)
{
  if (string.IsNullOrWhiteSpace(previousAgentPath))
  {
    logger.LogError(
      "Migration completed but no previous agent path was supplied, so the {PreviousBrandName} service was left installed. " +
      "Uninstall it manually.",
      previousBrandName);
    return;
  }

  if (!fileSystem.FileExists(previousAgentPath))
  {
    logger.LogError(
      "Migration completed but the previous agent executable was not found at {Path}. " +
      "The {PreviousBrandName} service was left installed. Uninstall it manually.",
      previousAgentPath,
      previousBrandName);
    return;
  }

  try
  {
    var processManager = services.GetRequiredService<IProcessManager>();

    // The old install still depends on the machine-wide values it set, and they are shared rather
    // than per-install, so clearing them would break the install that just replaced it.
    var arguments = "uninstall --preserve-machine-policy";
    if (!string.IsNullOrWhiteSpace(instanceId))
    {
      arguments += $" \"--instance-id\" \"{instanceId}\"";
    }

    var exitCode = await processManager.StartAndWaitForExit(
      previousAgentPath,
      arguments,
      false,
      TimeSpan.FromMinutes(10));

    if (exitCode != 0)
    {
      logger.LogError(
        "Retiring the {PreviousBrandName} install exited with code {ExitCode}. The new install is active; " +
        "the old service and its files need manual removal.",
        previousBrandName,
        exitCode);
    }
    else
    {
      logger.LogInformation("Retired the {PreviousBrandName} install.", previousBrandName);
    }
  }
  catch (Exception ex)
  {
    logger.LogError(
      ex,
      "Failed to retire the {PreviousBrandName} install. The new install is active; the old service and its files need manual removal.",
      previousBrandName);
  }
}

static Guid[]? ParseTagIds(string? deviceTags)
{
  if (deviceTags is null)
  {
    return null;
  }

  return [.. deviceTags
    .Split(",")
    .Select(x => Guid.TryParse(x, out var tagId)
      ? tagId
      : Guid.Empty)
    .Where(x => x != Guid.Empty)];
}

static void ValidateInstanceId(OptionResult optionResult)
{
  var id = optionResult.GetValueOrDefault<string?>();
  var validationError = Validators.ValidateInstanceId(id);
  if (validationError is not null)
  {
    optionResult.AddError(validationError);
  }
}
