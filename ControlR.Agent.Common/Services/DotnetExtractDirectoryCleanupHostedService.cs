using ControlR.Libraries.Branding;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

public class DotnetExtractDirectoryCleanupHostedService(
  IFileSystem fileSystem,
  IProcessManager processManager,
  IElevationChecker elevationChecker,
  ISystemEnvironment systemEnvironment,
  IFileSystemPathProvider fileSystemPathProvider,
  ILogger<DotnetExtractDirectoryCleanupHostedService> logger)
  : IHostedService
{
  private readonly IElevationChecker _elevationChecker = elevationChecker;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IFileSystemPathProvider _fileSystemPathProvider = fileSystemPathProvider;
  private readonly ILogger<DotnetExtractDirectoryCleanupHostedService> _logger = logger;
  private readonly IProcessManager _processManager = processManager;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;

  public Task StartAsync(CancellationToken cancellationToken)
  {
    try
    {
      if (!_elevationChecker.IsElevated())
      {
        _logger.LogDebug("Process is not elevated. Skipping .NET extraction directory cleanup.");
        return Task.CompletedTask;
      }

      var extractBaseDir = _fileSystemPathProvider.GetDotnetExtractDirectory();
      if (string.IsNullOrWhiteSpace(extractBaseDir))
      {
        _logger.LogDebug("Dotnet extract path is not set for platform {Platform}. Skipping cleanup.", _systemEnvironment.Platform);
        return Task.CompletedTask;
      }

      TryClearDotnetExtractDir(extractBaseDir);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while cleaning up .net extraction directory.");
    }

    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
    => Task.CompletedTask;

  private void TryClearDotnetExtractDir(string agentTempDirBase)
  {
    if (!_fileSystem.DirectoryExists(agentTempDirBase))
    {
      return;
    }

    var agentProcs = _processManager.GetProcessesByName(BrandingConstants.AgentBaseName).Length + 1;

    var subdirs = _fileSystem
      .GetDirectories(agentTempDirBase)
      .Select(_fileSystem.GetDirectoryInfo)
      .OrderByDescending(x => x.CreationTime)
      .Skip(Math.Max(1, agentProcs))
      .ToArray();

    foreach (var subdir in subdirs)
    {
      try
      {
        _logger.LogInformation("Deleting .NET extract subdirectory {SubDir}.", subdir.FullName);
        _fileSystem.DeleteDirectory(subdir.FullName, recursive: true);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to delete directory {SubDir}.", subdir);
      }
    }
  }
}
