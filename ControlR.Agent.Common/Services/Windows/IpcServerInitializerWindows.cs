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
  IIpcServerStore ipcStore,
  IProcessManager processManager,
  IIpcClientAuthenticator ipcAuthenticator,
  IHubConnection<IAgentHub> hubConnection,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<IpcServerInitializerWindows> logger)
  : IpcServerInitializerBase(timeProvider, ipcFactory, ipcStore, processManager, ipcAuthenticator, hubConnection,
    logger)
{
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
  private readonly int _sessionId = processManager.GetCurrentProcess().SessionId;

  protected override async Task<IIpcServer> CreateServer(string pipeName, CancellationToken cancellationToken)
  {
    if (_sessionId != 0)
    {
      var pipeServer = await IpcFactory.CreateServer(pipeName);
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

    // Deny access to anonymous users for security
    pipeSecurity.SetAccessRule(new PipeAccessRule(
      new SecurityIdentifier(WellKnownSidType.AnonymousSid, null),
      PipeAccessRights.FullControl,
      AccessControlType.Deny));

    return await IpcFactory.CreateServer(pipeName, pipeSecurity);
  }

  protected override string GetPipeName()
  {
    return IpcPipeNames.GetPipeName(_instanceOptions.Value.InstanceId);
  }
}