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
using ControlR.Libraries.Serilog;
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

  var installCommand = new Command(InstallCommandName, InstallCommandDescription)
  {
    serverUriOption,
    instanceIdOption,
    deviceTagsOption,
    tenantIdOption,
    installerKeySecretOption,
    installerKeyIdOption,
    deviceIdOption,
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
    };

    return await RunInstall(installRequest, parseResult.GetValue(instanceIdOption));
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

  uninstallCommand.SetAction(async parseResult => await RunUninstall(parseResult.GetValue(instanceIdOption)));

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

static async Task<int> RunInstall(AgentInstallRequest request, string? instanceId)
{
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
      return 1;
    }

    var metadata = metadataResult.Value;
    logger.LogInformation("Bundle version: {Version}", metadata.Version);

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
    };

    await installer.Install(installRequest);

    logger.LogInformation("Installation completed successfully.");
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
    new OptionsMonitorWrapper<InstanceOptions>(instanceOptions));
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
