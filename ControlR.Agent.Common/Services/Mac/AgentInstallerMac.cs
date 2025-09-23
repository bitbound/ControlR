using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Services.Base;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.NativeInterop.Unix;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Services.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Mac;

internal class AgentInstallerMac(
  IHostApplicationLifetime lifetime,
  IFileSystem fileSystem,
  ISystemEnvironment systemEnvironment,
  IServiceControl serviceControl,
  IRetryer retryer,
  IControlrApi controlrApi,
  IEmbeddedResourceAccessor embeddedResourceAccessor,
  IDeviceDataGenerator deviceDataGenerator,
  ISettingsProvider settingsProvider,
  IProcessManager processManager,
  IOptionsMonitor<AgentAppOptions> appOptions,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<AgentInstallerMac> logger)
  : AgentInstallerBase(fileSystem, controlrApi, deviceDataGenerator, settingsProvider, processManager, appOptions, logger), IAgentInstaller
{
  private static readonly SemaphoreSlim _installLock = new(1, 1);
  private readonly IEmbeddedResourceAccessor _embeddedResourceAccessor = embeddedResourceAccessor;
  private readonly ISystemEnvironment _environment = systemEnvironment;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
  private readonly IHostApplicationLifetime _lifetime = lifetime;
  private readonly ILogger<AgentInstallerMac> _logger = logger;
  private readonly IServiceControl _serviceControl = serviceControl;

  public async Task Install(
    Uri? serverUri = null,
    Guid? tenantId = null,
    string? installerKey = null,
    Guid[]? tags = null)
  {
    if (!await _installLock.WaitAsync(0))
    {
      _logger.LogWarning("Installer lock already acquired.  Aborting.");
      return;
    }

    try
    {
      if (serverUri is null && AppOptions.CurrentValue.ServerUri is null)
      {
        Logger.LogWarning(
          "The ServerUri needs to be provided either via command line arguments or installed appsettings file.  " +
          "Aborting installation.");
        return;
      }

      _logger.LogInformation("Install started.");

      if (Libc.Geteuid() != 0)
      {
        _logger.LogError("Install command must be run with sudo.");
        return;
      }

      var installDir = GetInstallDirectory();

      var exePath = _environment.StartupExePath;
      var fileName = Path.GetFileName(exePath);
      var targetPath = Path.Combine(installDir, AppConstants.GetAgentFileName(_environment.Platform));
      _fileSystem.CreateDirectory(installDir);

      if (_fileSystem.FileExists(targetPath))
      {
        _fileSystem.MoveFile(targetPath, $"{targetPath}.old", true);
      }

      _logger.LogInformation("Copying agent executable to {TargetPath}.", targetPath);
      await retryer.Retry(
        () =>
        {
          _fileSystem.CopyFile(exePath, targetPath, true);
          return Task.CompletedTask;
        },
        tryCount: 5,
        retryDelay: TimeSpan.FromSeconds(1));

      var agentPlistPath = GetLaunchDaemonFilePath();
      var agentPlistFile = (await GetLaunchDaemonFile()).Trim();
      var desktopPlistPath = GetLaunchAgentFilePath();
      var desktopPlistFile = (await GetLaunchAgentFile()).Trim();

      _logger.LogInformation("Writing plist files.");
      await _fileSystem.WriteAllTextAsync(agentPlistPath, agentPlistFile);
      await _fileSystem.WriteAllTextAsync(desktopPlistPath, desktopPlistFile);
      await UpdateAppSettings(serverUri, tenantId);

      var createResult = await CreateDeviceOnServer(installerKey, tags);
      if (!createResult.IsSuccess)
      {
        return;
      }

      await _serviceControl.StartAgentService(throwOnFailure: false);

      _logger.LogInformation("Installer finished.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while installing the ControlR service.");
    }
    finally
    {
      _lifetime.StopApplication();
      _installLock.Release();
    }
  }

  public async Task Uninstall()
  {
    if (!await _installLock.WaitAsync(0))
    {
      _logger.LogWarning("Installer lock already acquired.  Aborting.");
      return;
    }

    try
    {
      _logger.LogInformation("Uninstall started.");

      if (Libc.Geteuid() != 0)
      {
        _logger.LogError("Uninstall command must be run with sudo.");
      }

      var serviceFilePath = GetLaunchDaemonFilePath();
      var desktopFilePath = GetLaunchAgentFilePath();

       _logger.LogInformation("Booting out service.");
       await _serviceControl.StopAgentService(throwOnFailure: false);

      if (_fileSystem.FileExists(serviceFilePath))
      {
        _fileSystem.DeleteFile(serviceFilePath);
      }

      if (_fileSystem.FileExists(desktopFilePath))
      {
        _fileSystem.DeleteFile(desktopFilePath);
      }

      var installDir = GetInstallDirectory();
      if (_fileSystem.DirectoryExists(installDir))
      {
        _fileSystem.DeleteDirectory(installDir, true);
      }

      _logger.LogInformation("Uninstall completed.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while uninstalling the ControlR service.");
    }
    finally
    {
      _lifetime.StopApplication();
      _installLock.Release();
    }
  }

  private string GetAgentServiceName()
  {
    return string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId)
      ? "app.controlr.agent"
      : $"app.controlr.agent.{_instanceOptions.Value.InstanceId}";
  }

  private string GetInstallDirectory()
  {
    var dir = "/usr/local/bin/ControlR";
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return dir;
    }

    return Path.Combine(dir, _instanceOptions.Value.InstanceId);
  }

  private async Task<string> GetLaunchAgentFile()
  {
    var serviceName = string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId)
      ? "app.controlr.desktop"
      : $"app.controlr.desktop.{_instanceOptions.Value.InstanceId}";

    var template = await _embeddedResourceAccessor.GetResourceAsString(
      typeof(AgentInstallerMac).Assembly,
      "ControlR.Agent.Common.Resources.LaunchAgent.plist");

    template = template
      .Replace("{{SERVICE_NAME}}", serviceName)
      .Replace("{{INSTALL_DIRECTORY}}", GetInstallDirectory());

    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      // Remove lines containing {{INSTANCE_ID}} and <string>--instance-id</string>
      var lines = template.Split('\n');
      lines = [.. lines.Where(line =>
        !line.Contains("{{INSTANCE_ID}}") &&
        !line.Contains("<string>--instance-id</string>"))];

      template = string.Join("\n", lines);
    }
    else
    {
      template = template.Replace("{{INSTANCE_ID}}", _instanceOptions.Value.InstanceId);
    }
    return template;
  }

  private string GetLaunchAgentFilePath()
  {
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return "/Library/LaunchAgents/app.controlr.desktop.plist";
    }

    return $"/Library/LaunchAgents/app.controlr.desktop.{_instanceOptions.Value.InstanceId}.plist";
  }

  private async Task<string> GetLaunchDaemonFile()
  {
    var template = await _embeddedResourceAccessor.GetResourceAsString(
      typeof(AgentInstallerMac).Assembly,
      "ControlR.Agent.Common.Resources.LaunchDaemon.plist");

    template = template
      .Replace("{{SERVICE_NAME}}", GetAgentServiceName())
      .Replace("{{INSTALL_DIRECTORY}}", GetInstallDirectory());

    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      // Remove lines containing {{INSTANCE_ID}} and <string>-i</string>
      var lines = template.Split('\n');
      lines = [.. lines.Where(line =>
        !line.Contains("{{INSTANCE_ID}}") &&
        !line.Contains("<string>-i</string>"))];

      template = string.Join("\n", lines);
    }
    else
    {
      template = template.Replace("{{INSTANCE_ID}}", _instanceOptions.Value.InstanceId);
    }
    return template;
  }

  private string GetLaunchDaemonFilePath()
  {
    if (string.IsNullOrWhiteSpace(_instanceOptions.Value.InstanceId))
    {
      return "/Library/LaunchDaemons/app.controlr.agent.plist";
    }

    return $"/Library/LaunchDaemons/app.controlr.agent.{_instanceOptions.Value.InstanceId}.plist";
  }
}