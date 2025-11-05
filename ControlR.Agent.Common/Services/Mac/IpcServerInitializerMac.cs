using System.Runtime.Versioning;
using ControlR.Agent.Common.Interfaces;
using ControlR.Agent.Common.Services.Base;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.NativeInterop.Unix;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Mac;

[SupportedOSPlatform("macos")]
internal class IpcServerInitializerMac(
  TimeProvider timeProvider,
  IIpcConnectionFactory ipcFactory,
  IIpcServerStore desktopIpcStore,
  IProcessManager processManager,
  IFileSystem fileSystem,
  IFileSystemUnix fileSystemUnix,
  IIpcClientAuthenticator ipcAuthenticator,
  IHubConnection<IAgentHub> hubConnection,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<IpcServerInitializerMac> logger)
  : IpcServerInitializerBase(timeProvider, ipcFactory, desktopIpcStore, processManager, ipcAuthenticator, hubConnection, logger)
{
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IFileSystemUnix _fileSystemUnix = fileSystemUnix;
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;

  protected override string GetPipeName() => IpcPipeNames.GetPipeName(_instanceOptions.Value.InstanceId);

  protected override async Task<IIpcServer> CreateServer(string pipeName, CancellationToken cancellationToken)
  {
    var pipeServer = await IpcFactory.CreateServer(pipeName);
    SetPipePermissions(pipeName, pipeServer, cancellationToken).Forget();
    return pipeServer;
  }

  private async Task SetPipePermissions(string pipeName, IIpcServer pipeServer, CancellationToken cancellationToken)
  {
    var pipePath = pipeName.StartsWith('/')
      ? pipeName // Pipe name is absolute path.
      : $"/tmp/CoreFxPipe_{pipeName}";

    Logger.LogInformation("Starting pipe permission check for LaunchDaemon pipe {PipePath}", pipePath);

    // Wait for pipe file to get created.
    while (!pipeServer.IsDisposed && !cancellationToken.IsCancellationRequested)
    {
      if (_fileSystem.FileExists(pipePath))
      {
        break;
      }

      await Task.Delay(TimeSpan.FromSeconds(2), TimeProvider, cancellationToken);
    }

    try
    {
      // Check current permissions
      var currentMode = File.GetUnixFileMode(pipePath);
      var requiredMode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                        UnixFileMode.GroupRead | UnixFileMode.GroupWrite;

      if (currentMode != requiredMode)
      {
        Logger.LogInformation(
          "Required pipe permissions of {RequiredMode} do not match current permissions {CurrentMode}. Fixing...",
          requiredMode,
          currentMode);

        // Set the required permissions
        _fileSystem.SetUnixFileMode(pipePath, requiredMode);
      }

      // Check and set group ownership to staff group if needed
      try
      {
        var currentGroup = _fileSystemUnix.GetFileGroup(pipePath);
        Logger.LogInformation("Current group for pipe {PipePath}: {CurrentGroup}", pipePath, currentGroup ?? "null");

        if (string.Equals(currentGroup, "staff", StringComparison.OrdinalIgnoreCase))
        {
          // Already owned by staff group, no need to change
          Logger.LogInformation("Pipe {PipePath} is already owned by staff group", pipePath);
        }
        else
        {
          Logger.LogInformation("Setting group ownership of pipe {PipePath} from {CurrentGroup} to staff", pipePath, currentGroup ?? "null");
          var setResult = _fileSystemUnix.SetFileGroup(pipePath, "staff");

          if (setResult)
          {
            Logger.LogInformation("Successfully set group ownership of pipe {PipePath} to staff group", pipePath);
          }
          else
          {
            Logger.LogWarning("Failed to set group ownership of pipe {PipePath} to staff group.", pipePath);
          }
        }
      }
      catch (Exception ex)
      {
        Logger.LogWarning(ex, "Error setting group ownership of pipe {PipePath} to staff group",
          pipePath);
      }
    }
    catch (OperationCanceledException)
    {
      // Normal when host is shutting down.
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "Error during pipe permission check for {PipePath}", pipePath);
    }
  }
}
