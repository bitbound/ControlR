using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.Options;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ControlR.Libraries.DevicesCommon.Services.Processes;

namespace ControlR.DesktopCli.Services;

public class IpcClientManager(
  TimeProvider timeProvider,
  IIpcConnectionFactory ipcConnectionFactory,
  IProcessManager processManager,
  IOptions<DesktopClientOptions> options,
  ILogger<IpcClientManager> logger) : BackgroundService
{
  private readonly IOptions<DesktopClientOptions> _options = options;
  private readonly ILogger<IpcClientManager> _logger = logger;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IIpcConnectionFactory _ipcConnectionFactory = ipcConnectionFactory;
  private readonly IProcessManager _processManager = processManager;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var processId = _processManager.GetCurrentProcess().Id;
    var pipeName = IpcPipeNames.GetPipeName();

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        _logger.LogInformation("Attempting to connect to IPC server. Pipe Name: {pipeName}", pipeName);

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
      catch (OperationCanceledException)
      {
        continue;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while connecting to IPC server.");
      }

      await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, stoppingToken);
    }
    
    _logger.LogInformation("IpcClientManager stopping.");
  }

  private async void HandleRemoteControlRequest(RemoteControlRequestIpcDto dto)
  {
    try
    {
      _logger.LogInformation(
        "Handling remote control request. Session ID: {SessionId}, Viewer Connection ID: {ViewerConnectionId}, " +
        "Target System Session: {TargetSystemSession}, Process ID: {TargetProcessId}, Viewer Name: {ViewerName}",
        dto.SessionId,
        dto.ViewerConnectionId,
        dto.TargetSystemSession,
        dto.TargetProcessId,
        dto.ViewerName);

      var builder = Host.CreateApplicationBuilder();
      builder.AddCommonDesktopServices(options =>
      {
        options.WebSocketUri = dto.WebsocketUri;
        options.SessionId = dto.SessionId;
        options.NotifyUser = dto.NotifyUserOnSessionStart;
        options.ViewerName = dto.ViewerName;
      });

#if WINDOWS_BUILD
      builder.AddWindowsDesktopServices(dto.DataFolder);
#elif MAC_BUILD
      builder.AddMacDesktopServices(dto.DataFolder);
#elif LINUX_BUILD
      builder.AddLinuxDesktopServices(dto.DataFolder);
#else
      throw new PlatformNotSupportedException("This platform is not supported. Supported platforms are Windows, MacOS, and Linux.");
#endif

      using var app = builder.Build();
      await app.RunAsync();

      _logger.LogInformation(
        "Remote control session finished. Session ID: {SessionId}, Viewer Connection ID: {ViewerConnectionId}, " +
        "Target System Session: {TargetSystemSession}, Process ID: {TargetProcessId}, Viewer Name: {ViewerName}",
        dto.SessionId,
        dto.ViewerConnectionId,
        dto.TargetSystemSession,
        dto.TargetProcessId,
        dto.ViewerName);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling remote control request.");
    }
    finally
    {
      GC.Collect();
      GC.WaitForPendingFinalizers();
    }
  }
}
