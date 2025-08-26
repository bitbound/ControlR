using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services.Base;

internal abstract class IpcServerInitializerBase(
    TimeProvider timeProvider,
    IIpcConnectionFactory ipcFactory,
    IIpcServerStore ipcStore,
    IProcessManager processManager,
    IAgentHubConnection agentHubConnection,
    ILogger logger) : BackgroundService
{
  protected readonly TimeProvider _timeProvider = timeProvider;
  protected readonly IIpcConnectionFactory _ipcFactory = ipcFactory;
  protected readonly IIpcServerStore _ipcStore = ipcStore;
  protected readonly IProcessManager _processManager = processManager;
  protected readonly IAgentHubConnection _agentHubConnection = agentHubConnection;
  protected readonly ILogger _logger = logger;

  protected async Task HandleConnection(IIpcServer server, CancellationToken cancellationToken)
  {
    try
    {
      _logger.LogInformation("Accepted IPC connection.  Waiting for PID attestation.");
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
      using var signal = new ManualResetEventAsync();
      server.On<IpcClientIdentityAttestationDto>(dto =>
      {
        try
        {
          _logger.LogInformation("Received IPC client identity attestation for process {ProcessId}.", dto.ProcessId);
          var process = _processManager.GetProcessById(dto.ProcessId);
          _ipcStore.AddServer(process, server);
          signal.Set();
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error while processing IPC client identity attestation for process {ProcessId}.", dto.ProcessId);
        }
      });

      server.On<ChatResponseIpcDto>(dto =>
      {
        try
        {
          Task.Run(async () =>
          {
            var hubDto = new ChatResponseHubDto(
              dto.SessionId,
              dto.Message,
              dto.SenderUsername,
              dto.ViewerConnectionId,
              dto.Timestamp);
            await _agentHubConnection.SendChatResponse(hubDto);
          });
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error while handling chat response for session {SessionId}.", dto.SessionId);
        }
      });
      server.BeginRead(cancellationToken);
      await signal.Wait(linkedCts.Token);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling IPC connection.");
      Disposer.DisposeAll(server);
    }
  }
}
