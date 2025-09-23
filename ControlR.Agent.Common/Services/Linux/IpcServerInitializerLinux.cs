using System.Runtime.Versioning;
using ControlR.Agent.Common.Services.Base;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.NativeInterop.Unix;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Linux;

[SupportedOSPlatform("linux")]
internal class IpcServerInitializerLinux(
  TimeProvider timeProvider,
  IIpcConnectionFactory ipcFactory,
  IIpcServerStore desktopIpcStore,
  IProcessManager processManager,
  IFileSystem fileSystem,
  IHubConnection<IAgentHub> hubConnection,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<IpcServerInitializerLinux> logger) 
  : IpcServerInitializerBase(timeProvider, ipcFactory, desktopIpcStore, processManager, hubConnection, logger)
{
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3), _timeProvider);

    try
    {
      while (await timer.WaitForNextTickAsync(stoppingToken))
      {
        try
        {
          await AcceptConnection(stoppingToken);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error while accepting IPC connections.");
        }
      }
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogInformation(ex, "Stopping IPC server. Application is shutting down.");
    }
  }

  private async Task AcceptConnection(CancellationToken cancellationToken)
  {
    try
    {
      var pipeName = IpcPipeNames.GetPipeName(_instanceOptions.Value.InstanceId);
      _logger.LogInformation("Creating IPC server for pipe: {PipeName}", pipeName);
      var server = await CreateServer(pipeName, cancellationToken);
      _logger.LogInformation("Waiting for incoming IPC connection.");

      if (!await server.WaitForConnection(cancellationToken))
      {
        _logger.LogWarning("Failed to accept incoming IPC connection.");
        return;
      }

      HandleConnection(server, cancellationToken).Forget();
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogInformation(ex, "Stopping IPC server. Application is shutting down.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while accepting IPC connection.");
      return;
    }
  }

  private async Task<IIpcServer> CreateServer(string pipeName, CancellationToken cancellationToken)
  {
    var pipeServer = await _ipcFactory.CreateServer(pipeName);
    SetPipePermissions(pipeName, pipeServer, cancellationToken).Forget();
    return pipeServer;
  }

  private async Task SetPipePermissions(string pipeName, IIpcServer pipeServer, CancellationToken cancellationToken)
  {
    var pipePath = pipeName.StartsWith('/')
      ? pipeName // Pipe name is absolute path.
      : $"/tmp/CoreFxPipe_{pipeName}";

    _logger.LogInformation("Starting pipe permission check for systemd service pipe {PipePath}", pipePath);

    // Wait for pipe file to get created.
    while (!pipeServer.IsDisposed && !cancellationToken.IsCancellationRequested)
    {
      if (_fileSystem.FileExists(pipePath))
      {
        break;
      }

      await Task.Delay(TimeSpan.FromSeconds(2), _timeProvider, cancellationToken);
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
        _logger.LogInformation(
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
      _logger.LogWarning(ex, "Error during pipe permission check for {PipePath}", pipePath);
    }
  }
}