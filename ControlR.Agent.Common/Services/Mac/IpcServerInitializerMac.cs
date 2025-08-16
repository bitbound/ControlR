using System.Runtime.Versioning;
using ControlR.Agent.Common.Services.Base;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.NativeInterop.Unix;

namespace ControlR.Agent.Common.Services.Mac;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal class IpcServerInitializerMac(
  TimeProvider timeProvider,
  IIpcConnectionFactory ipcFactory,
  IIpcServerStore desktopIpcStore,
  IProcessManager processManager,
  IFileSystem fileSystem,
  IFileSystemUnix fileSystemUnix,
  ILogger<IpcServerInitializerMac> logger) 
  : IpcServerInitializerBase(timeProvider, ipcFactory, desktopIpcStore, processManager, logger)
{
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IFileSystemUnix _fileSystemUnix = fileSystemUnix;

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
      var pipeName = IpcPipeNames.GetMacPipeName();
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
    MonitorPipePermissions(pipeName, pipeServer, cancellationToken).Forget();
    return pipeServer;
  }

  private async Task MonitorPipePermissions(string pipeName, IIpcServer pipeServer, CancellationToken cancellationToken)
  {
    var pipePath = pipeName.StartsWith('/')
      ? pipeName // Pipe name is absolute path.
      : $"/tmp/CoreFxPipe_{pipeName}";

    _logger.LogInformation("Starting continuous permission monitoring for LaunchDaemon pipe {PipePath}", pipePath);


    while (!pipeServer.IsDisposed && !cancellationToken.IsCancellationRequested)
    {
      try
      {
        if (_fileSystem.FileExists(pipePath))
        {
          // Check current permissions
          var currentMode = File.GetUnixFileMode(pipePath);
          var requiredMode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                            UnixFileMode.GroupRead | UnixFileMode.GroupWrite;

          if (currentMode != requiredMode)
          {
            _logger.LogWarning(
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
            _logger.LogDebug("Current group for pipe {PipePath}: {CurrentGroup}", pipePath, currentGroup ?? "null");
            
            if (string.Equals(currentGroup, "staff", StringComparison.OrdinalIgnoreCase))
            {
              // Already owned by staff group, no need to change
              _logger.LogDebug("Pipe {PipePath} is already owned by staff group", pipePath);
            }
            else
            {
              _logger.LogInformation("Setting group ownership of pipe {PipePath} from {CurrentGroup} to staff", pipePath, currentGroup ?? "null");
              var setResult = _fileSystemUnix.SetFileGroup(pipePath, "staff");
              
              if (setResult)
              {
                _logger.LogDebug("Successfully set group ownership of pipe {PipePath} to staff group", pipePath);
              }
              else
              {
                _logger.LogWarning("Failed to set group ownership of pipe {PipePath} to staff group.", pipePath);
              }
            }
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Error setting group ownership of pipe {PipePath} to everyone group", 
              pipePath);
          }
        }

        // Check every 2 seconds
        await Task.Delay(TimeSpan.FromSeconds(2), _timeProvider, cancellationToken);
      }
      catch (OperationCanceledException)
      {
        break;
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Error during continuous permission monitoring for {PipePath}", pipePath);
        await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, cancellationToken); // Wait longer on error
      }
    }

    _logger.LogInformation("Stopped continuous permission monitoring for LaunchDaemon pipe {PipePath}", pipePath);
  }
}
