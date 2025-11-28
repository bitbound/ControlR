using System.Runtime.Versioning;
using ControlR.Agent.Common.Services.Base;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Linux;

[SupportedOSPlatform("linux")]
internal class IpcServerInitializerLinux(
  TimeProvider timeProvider,
  IIpcConnectionFactory ipcFactory,
  IIpcServerStore desktopIpcStore,
  IProcessManager processManager,
  IFileSystem fileSystem,
  IIpcClientAuthenticator ipcAuthenticator,
  IHubConnection<IAgentHub> hubConnection,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<IpcServerInitializerLinux> logger)
  : IpcServerInitializerBase(timeProvider, ipcFactory, desktopIpcStore, processManager, ipcAuthenticator, hubConnection, logger)
{
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;

  protected override async Task<IIpcServer> CreateServer(string pipeName, CancellationToken cancellationToken)
  {
    var pipeServer = await IpcFactory.CreateServer(pipeName);
    SetPipePermissions(pipeName, pipeServer, cancellationToken).Forget();
    return pipeServer;
  }
  protected override string GetPipeName() => IpcPipeNames.GetPipeName(_instanceOptions.Value.InstanceId);

  private async Task SetPipePermissions(string pipeName, IIpcServer pipeServer, CancellationToken cancellationToken)
  {
    var pipePath = pipeName.StartsWith('/')
      ? pipeName // Pipe name is absolute path.
      : $"/tmp/CoreFxPipe_{pipeName}";

    Logger.LogInformation("Starting pipe permission check for systemd service pipe {PipePath}", pipePath);

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
                        UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                        UnixFileMode.OtherRead | UnixFileMode.OtherWrite;

      if (currentMode != requiredMode)
      {
        Logger.LogInformation(
          "Required pipe permissions of {RequiredMode} do not match current permissions {CurrentMode}. Fixing...",
          requiredMode,
          currentMode);

        // Set the required permissions
        _fileSystem.SetUnixFileMode(pipePath, requiredMode);
      }
      // In the future, we can add a user-supplied group and chown to that group.
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