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
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.DataValidation;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.FileSystem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Parsing;

const string RootDescription = $"{BrandingConstants.BrandName} agent installer.";
const string InstallCommandName = "install";
const string RepairDesktopCommandName = "repair-desktop";
const string UninstallCommandName = "uninstall";
const string InstallCommandDescription = $"Install the {BrandingConstants.BrandName} agent bundle.";
const string RepairDesktopCommandDescription = "Repair the installed desktop client payload without modifying the agent service.";
const string UninstallCommandDescription = $"Uninstall the {BrandingConstants.BrandName} agent bundle.";
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
const string PreviousBrandNameDescription = "Brand name of the agent install being replaced. Set only during a cross-brand migration. That install is removed once this brand is installed and running. The server is what authorizes the migration.";
const string PreviousBrandNameLongAlias = "--previous-brand-name";
const string PreviousInstanceIdDescription = "Instance ID of the agent install being replaced, set only during a migration. Omit when that install used the default instance ID. That install is removed once this brand is installed and running.";
const string PreviousInstanceIdLongAlias = "--previous-instance-id";
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

  var previousInstanceIdOption = new Option<string?>(PreviousInstanceIdLongAlias)
  {
    Required = false,
    Description = PreviousInstanceIdDescription,
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
    previousInstanceIdOption,
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
      parseResult.GetValue(previousInstanceIdOption));
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

  var uninstallCommand = new Command(UninstallCommandName, UninstallCommandDescription)
  {
    instanceIdOption,
  };

  uninstallCommand.SetAction(async parseResult => await RunUninstall(
    parseResult.GetValue(instanceIdOption)));

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
  string? previousBrandArg,
  string? previousInstanceIdArg)
{
  using var host = CreateInstallerHost(instanceId, request.ServerUri);
  var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ControlR.Agent.Installer");
  using var logScope = logger.BeginScope("RunInstall. InstanceId: {InstanceId}", instanceId);
  var fileSystem = host.Services.GetRequiredService<IFileSystem>();
  var tempDir = Path.Combine(Path.GetTempPath(), $"{TempDirectoryPrefix}{Guid.NewGuid():N}");
  var tempBundlePath = Path.Combine(tempDir, TempBundleFileName);

  // Declared out here so the catch can roll the replaced install back too.
  string? previousBrand = null;
  string? previousInstanceId = null;

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
      return 1;
    }

    var metadata = metadataResult.Value;
    logger.LogInformation("Bundle version: {Version}", metadata.Version);

    // The server this install is pointed at is the authority. If its bundle names a different brand,
    // this installer is not the right binary to be running here. Brands are compared by the key that
    // names every directory, service, and registry key, so two names that reduce to the same key are
    // the same install and this installer is still the right binary.
    if (!BrandNames.AreSameInstall(metadata.BrandName, BrandingConstants.BrandName))
    {
      logger.LogCritical(
        "Refusing to install: server bundle is for brand {ServerBrandName}, but this installer is brand {InstallerBrandName}. " +
        "Nothing on this machine was changed.",
        metadata.BrandName,
        BrandingConstants.BrandName);
      return 1;
    }

    // A migration is indicated by the install being replaced, which the resident install passes in.
    var previousInstall = PreviousInstall.Resolve(
      previousBrandArg,
      previousInstanceIdArg,
      BrandingConstants.BrandName,
      instanceId);

    if (previousInstall is null)
    {
      if (!string.IsNullOrWhiteSpace(previousBrandArg) || !string.IsNullOrWhiteSpace(previousInstanceIdArg))
      {
        logger.LogWarning("Ignoring the previous-install arguments because they describe this same install.");
      }
    }
    else
    {
      previousBrand = previousInstall.BrandName;
      previousInstanceId = previousInstall.InstanceId;
    }

    if (previousBrand is not null)
    {
      // The replacing install writes its settings into this brand's directory before this runs, and
      // those settings are what carry the device ID and signing key across. Without them this install
      // registers a new device and signs with a key the server has never been told about.
      var optionsAccessor = host.Services.GetRequiredService<IOptionsAccessor>();
      if (optionsAccessor.DeviceId == Guid.Empty || string.IsNullOrWhiteSpace(optionsAccessor.PrivateKey))
      {
        logger.LogCritical(
          "Refusing to migrate from brand {PreviousBrandName}: this brand's settings file carries no device ID or no signing key, " +
          "so this install would register a new device and the existing one could never authenticate again. Nothing on this machine was changed.",
          previousBrand);
        return 1;
      }

      logger.LogWarning(
        "Migrating install from brand {PreviousBrandName} (instance id {PreviousInstanceId}) to {InstallerBrandName} (instance id {InstallerInstanceId}). Device identity and signing key carry over from the settings file already staged for this brand.",
        previousBrand,
        previousInstanceId,
        BrandingConstants.BrandName,
        GetEffectiveInstanceId(instanceId));
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
      PreviousBrandName = previousBrand,
      PreviousInstanceId = previousInstanceId,
    };

    var installResult = await installer.Install(installRequest);
    if (!installResult.IsSuccess)
    {
      logger.LogError("Installation failed. Reason: {Reason}", installResult.Reason);
      await RecoverFromFailedInstall(host, installer, logger, previousBrand, previousInstanceId);
      return 1;
    }

    if (installResult.Value == AgentInstallOutcome.HandedOff)
    {
      // This process copied itself to a temp directory and started that copy, which owns the result.
      // Rolling back here would put the replaced install back while the copy is still replacing it,
      // and retiring here would remove the install that copy is still working from.
      logger.LogInformation("This process handed the install to a temp copy, which will report its own result.");
      return 0;
    }

    logger.LogInformation("Installation completed successfully.");

    if (previousBrand is not null)
    {
      // Best effort. This install is already working, so a failure here must not report a failed
      // install. A leftover old-brand service is recoverable; reporting success as failure is not.
      await installer.RetirePreviousBrand(previousBrand, previousInstanceId);
    }

    return 0;
  }
  catch (Exception ex)
  {
    logger.LogError(ex, "Installation failed.");

    var installer = host.Services.GetRequiredService<IAgentInstaller>();
    await RecoverFromFailedInstall(host, installer, logger, previousBrand, previousInstanceId);
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

/// <summary>
/// Puts a running agent back after a failed install. Every install stops a service before replacing
/// files, so a failure part-way through could otherwise leave the machine with nothing running and no
/// way in except a physical visit.
/// </summary>
static async Task RecoverFromFailedInstall(
  IHost host,
  IAgentInstaller installer,
  ILogger logger,
  string? previousBrand,
  string? previousInstanceId)
{
  try
  {
    if (previousBrand is not null)
    {
      await installer.RestorePreviousBrand(previousBrand, previousInstanceId);
      return;
    }

    logger.LogWarning("Install did not complete. Starting this brand's service from what is already on disk.");
    await host.Services.GetRequiredService<IServiceControl>().StartAgentService(throwOnFailure: false);
  }
  catch (Exception ex)
  {
    logger.LogError(ex, "Failed to put a running agent back after the failed install.");
  }
}

static async Task<int> RunUninstall(string? instanceId)
{
  using var host = CreateInstallerHost(instanceId, serverUri: null);
  var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ControlR.Agent.Installer");
  var installer = host.Services.GetRequiredService<IAgentInstaller>();

  try
  {
    await installer.Uninstall();
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
    if (!string.Equals(metadata.BrandName, BrandingConstants.BrandName, StringComparison.Ordinal))
    {
      logger.LogCritical(
        "Refusing to repair desktop client: server bundle is for brand {ServerBrandName}, but this installer is brand {InstallerBrandName}. " +
        "If this server is correct, run the matching installer instead.",
        metadata.BrandName,
        BrandingConstants.BrandName);
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

static string GetEffectiveInstanceId(string? instanceId)
{
  return string.IsNullOrWhiteSpace(instanceId) ? AppConstants.DefaultInstanceId : instanceId;
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
