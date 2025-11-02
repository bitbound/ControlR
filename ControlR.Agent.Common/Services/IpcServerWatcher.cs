using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

internal class IpcServerWatcher(
  IIpcServerStore ipcServerStore,
  ILogger<IpcServerWatcher> logger) : BackgroundService
{
  private readonly IIpcServerStore _ipcServerStore = ipcServerStore;
  private readonly ILogger<IpcServerWatcher> _logger = logger;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
    while (await timer.WaitForNextTick(throwOnCancellation: false, stoppingToken))
    {
      foreach (var kvp in _ipcServerStore.Servers)
      {
        try
        {
          var server = kvp.Value;

          if (server.Process?.HasExited == true || !server.Server.IsConnected)
          {
            _logger.LogInformation(
              "Removing IPC session for process {ProcessId}. Process exited: {HasExited}, Connected: {IsConnected}",
              server.Process?.Id,
              server.Process?.HasExited,
              server.Server.IsConnected);

            if (_ipcServerStore.TryRemove(kvp.Key, out var serverRecord))
            {
              Disposer.DisposeAll(serverRecord.Process, serverRecord.Server);
            }
          }
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error while checking IPC server processes for exit.");
        }
      }
    }

    _logger.LogInformation("Ipc server watcher stopped. Application is shutting down.");
    await _ipcServerStore.KillAllServers("Agent is shutting down.");
  }
}