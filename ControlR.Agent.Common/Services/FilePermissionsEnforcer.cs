using System.Security.Principal;
using ControlR.Libraries.Hosting;
using ControlR.Libraries.Shared.Services.FileSystem;

namespace ControlR.Agent.Common.Services;

public class FilePermissionsEnforcer(
  TimeProvider timeProvider,
  IFileAccessPermissions fileAccessPermissions,
  IFileSystemPathProvider fileSystemPathProvider,
  IElevationChecker elevationChecker,
  ILogger<FilePermissionsEnforcer> logger)
  : PeriodicBackgroundService(TimeSpan.FromMinutes(30), true, timeProvider, logger)
{

  protected override Task HandleElapsed()
  {
    EnforceAccess();
    return Task.CompletedTask;
  }

  protected override Task OnStartingAsync(CancellationToken stoppingToken)
  {
    EnforceAccess();
    return base.OnStartingAsync(stoppingToken);
  }


  private void EnforceAccess()
  {
    logger.LogDebug("Enforcing file permissions on sensitive files.");

    if (!elevationChecker.IsElevated())
    {
      logger.LogDebug("Skipping file permissions enforcement because the agent is not running with elevated privileges.");
      return;
    }

    var appSettingsPath = fileSystemPathProvider.GetAgentAppSettingsPath();
    if (OperatingSystem.IsWindows())
    {
      fileAccessPermissions.Set(
        filePath: appSettingsPath,
        includeCurrentUser: true,
        isProtected: true,
        preserveInheritance: false,
        owner: WellKnownSidType.BuiltinAdministratorsSid,
        allowedSids: [WellKnownSidType.BuiltinAdministratorsSid, WellKnownSidType.LocalSystemSid]);
    }
    else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
    {
      fileAccessPermissions.Set(appSettingsPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
    else
    {
      throw new PlatformNotSupportedException("Unsupported operating system for setting file permissions.");
    }
    logger.LogDebug("File permissions enforcement completed.");
  }
}