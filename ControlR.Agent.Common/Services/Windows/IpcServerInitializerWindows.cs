using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using ControlR.Agent.Common.Services.Base;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services.Windows;

[SupportedOSPlatform("windows")]
internal class IpcServerInitializerWindows(
  TimeProvider timeProvider,
  IIpcConnectionFactory ipcFactory,
  IIpcServerStore desktopIpcStore,
  IProcessManager processManager,
  IHubConnection<IAgentHub> hubConnection,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<IpcServerInitializerWindows> logger) 
  : IpcServerInitializerBase(timeProvider, ipcFactory, desktopIpcStore, processManager, hubConnection, logger)
{
  private readonly int _sessionId = processManager.GetCurrentProcess().SessionId;
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
      _logger.LogInformation(ex, "Stopping IPC server.  Application is shutting down.");
    }
  }

  private async Task AcceptConnection(CancellationToken cancellationToken)
  {
    try
    {
      var pipeName = IpcPipeNames.GetPipeName(_instanceOptions.Value.InstanceId);
      _logger.LogInformation("Creating IPC server for pipe: {PipeName}", pipeName);
      var server = await CreateServer(pipeName);
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
      _logger.LogInformation(ex, "Stopping IPC server.  Application is shutting down.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while accepting IPC connection.");
      return;
    }
  }

  private async Task<IIpcServer> CreateServer(string pipeName)
  {
    if (_sessionId != 0)
    {
      var pipeServer = await _ipcFactory.CreateServer(pipeName);
      return pipeServer;
    }

    var pipeSecurity = new PipeSecurity();

    // Allow full control to SYSTEM
    pipeSecurity.SetAccessRule(new PipeAccessRule(
      new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
      PipeAccessRights.FullControl,
      AccessControlType.Allow));

    // Allow full control to interactive users (logged-in users)
    pipeSecurity.SetAccessRule(new PipeAccessRule(
      new SecurityIdentifier(WellKnownSidType.InteractiveSid, null),
      PipeAccessRights.FullControl,
      AccessControlType.Allow));

    // Allow full control to authenticated users
    pipeSecurity.SetAccessRule(new PipeAccessRule(
      new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
      PipeAccessRights.FullControl,
      AccessControlType.Allow));

    // Deny access to anonymous users for security
    pipeSecurity.SetAccessRule(new PipeAccessRule(
      new SecurityIdentifier(WellKnownSidType.AnonymousSid, null),
      PipeAccessRights.FullControl,
      AccessControlType.Deny));

    return await _ipcFactory.CreateServer(pipeName, pipeSecurity);
  }
}
