using ControlR.DesktopClient.Common;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Services;

public class IpcClientManager(
  TimeProvider timeProvider,
  IRemoteControlHostManager remoteControlHostManager,
  IIpcConnectionFactory ipcConnectionFactory,
  IProcessManager processManager,
  ILogger<IpcClientManager> logger) : BackgroundService
{
  private readonly IIpcConnectionFactory _ipcConnectionFactory = ipcConnectionFactory;
  private readonly IRemoteControlHostManager _remoteControlHostManager = remoteControlHostManager;
  private readonly ILogger<IpcClientManager> _logger = logger;
  private readonly IProcessManager _processManager = processManager;
  private readonly TimeProvider _timeProvider = timeProvider;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    await AcceptClientConnections(stoppingToken);
  }

  private async Task AcceptClientConnections(CancellationToken stoppingToken)
  {
    var processId = _processManager.GetCurrentProcess().Id;
    var pipeName = IpcPipeNames.GetPipeName();

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        _logger.LogInformation("Attempting to connect to IPC server. Pipe Name: {PipeName}", pipeName);

        using var client = await _ipcConnectionFactory.CreateClient(".", pipeName);
        client.On<RemoteControlRequestIpcDto>(HandleRemoteControlRequest);

        if (!await client.Connect(stoppingToken))
        {
          _logger.LogWarning("Failed to connect to IPC server.");
          await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, stoppingToken);
          continue;
        }

        _logger.LogInformation("Connected to IPC server.");
        client.BeginRead(stoppingToken);
        _logger.LogInformation("Read started.");

        _logger.LogInformation("Sending client identity attestation. Process ID: {ProcessId}", processId);
        var dto = new IpcClientIdentityAttestationDto(processId);
        await client.Send(dto, stoppingToken);

        _logger.LogInformation("Waiting for connection end.");
        await client.WaitForConnectionEnd(stoppingToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while connecting to IPC server.");
      }

      await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, stoppingToken);
    }
  }

  private void HandleRemoteControlRequest(RemoteControlRequestIpcDto dto)
  {
     _remoteControlHostManager.StartHost(dto).Forget();
  }
}