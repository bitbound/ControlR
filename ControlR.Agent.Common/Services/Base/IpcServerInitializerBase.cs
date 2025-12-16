using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Agent.Common.Interfaces;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services.Base;

internal abstract class IpcServerInitializerBase(
  TimeProvider timeProvider,
  IIpcConnectionFactory ipcFactory,
  IIpcServerStore ipcStore,
  IProcessManager processManager,
  IIpcClientAuthenticator ipcAuthenticator,
  IHubConnection<IAgentHub> hubConnection,
  ILogger logger) : BackgroundService
{
  protected IHubConnection<IAgentHub> HubConnection { get; } = hubConnection;
  protected IIpcClientAuthenticator IpcAuthenticator { get; } = ipcAuthenticator;
  protected IIpcConnectionFactory IpcFactory { get; } = ipcFactory;
  protected IIpcServerStore IpcStore { get; } = ipcStore;
  protected ILogger Logger { get; } = logger;
  protected IProcessManager ProcessManager { get; } = processManager;
  protected TimeProvider TimeProvider { get; } = timeProvider;

  protected virtual async Task AcceptConnection(CancellationToken cancellationToken)
  {
    try
    {
      var pipeName = GetPipeName();
      Logger.LogInformation("Creating IPC server for pipe: {PipeName}", pipeName);
      var server = await CreateServer(pipeName, cancellationToken);
      Logger.LogInformation("Waiting for incoming IPC connection.");

      if (!await server.WaitForConnection(cancellationToken))
      {
        Logger.LogWarning("Failed to accept incoming IPC connection.");
        return;
      }

      // Authenticate the connection
      var authResult = await IpcAuthenticator.AuthenticateConnection(server);
      if (!authResult.IsSuccess)
      {
        Logger.LogCritical(
          "IPC connection authentication FAILED: {Reason}. Connection rejected and disconnected.",
          authResult.Reason);

        // TODO: Send authentication failure event to server's event notification system
        // once that feature is implemented. Include: timestamp, attempted process ID,
        // executable path, and failure reason.

        server.Dispose();
        return;
      }

      Logger.LogInformation("IPC connection authenticated successfully.");
      HandleConnection(server, authResult.Value, cancellationToken).Forget();
    }
    catch (OperationCanceledException ex)
    {
      Logger.LogInformation(ex, "Stopping IPC server. Application is shutting down.");
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while accepting IPC connection.");
    }
  }
  protected abstract Task<IIpcServer> CreateServer(string pipeName, CancellationToken cancellationToken);
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3), TimeProvider);

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
          Logger.LogError(ex, "Error while accepting IPC connections.");
        }
      }
    }
    catch (OperationCanceledException ex)
    {
      Logger.LogInformation(ex, "Stopping IPC server. Application is shutting down.");
    }
  }
  protected abstract string GetPipeName();
  protected Task HandleConnection(IIpcServer server, ClientCredentials credentials, CancellationToken cancellationToken)
  {
    try
    {
      Logger.LogInformation("Setting up IPC connection for process {ProcessId}.", credentials.ProcessId);
      var process = ProcessManager.GetProcessById(credentials.ProcessId);
      IpcStore.AddServer(process, server);

      server.Start();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while handling IPC connection.");
      Disposer.DisposeAll(server);
    }

    return Task.CompletedTask;
  }
}