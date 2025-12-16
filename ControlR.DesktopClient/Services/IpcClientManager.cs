using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls.ApplicationLifetimes;
using ControlR.DesktopClient.Common.Options;
using ControlR.Libraries.Ipc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Services;

public class IpcClientManager(
  TimeProvider timeProvider,
  IIpcConnectionFactory ipcConnectionFactory,
  IControlledApplicationLifetime appLifetime,
  IOptions<DesktopClientOptions> desktopClientOptions,
  ILogger<IpcClientManager> logger) : BackgroundService, IIpcClientAccessor
{
  private readonly IControlledApplicationLifetime _appLifetime = appLifetime;
  private readonly TimeSpan _connectionTimeout = TimeSpan.FromSeconds(60);
  private readonly IOptions<DesktopClientOptions> _desktopClientOptions = desktopClientOptions;
  private readonly IIpcConnectionFactory _ipcConnectionFactory = ipcConnectionFactory;
  private readonly ILogger<IpcClientManager> _logger = logger;
  private readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(5);
  private readonly TimeProvider _timeProvider = timeProvider;

  private IIpcClient? _client;
  private DateTimeOffset? _firstFailedAttempt;

  public bool TryGetClient([NotNullWhen(true)] out IIpcClient? client)
  {
    client = _client;
    return client is not null;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    await CreateClientConnection(stoppingToken);
  }

  private bool CheckIfShouldShutdown()
  {
    if (!_firstFailedAttempt.HasValue)
      return false;

    _logger.LogWarning("Failed to connect to IPC server.");

    // Check if we've exceeded the connection timeout
    var elapsed = _timeProvider.GetUtcNow() - _firstFailedAttempt.Value;
    if (elapsed > _connectionTimeout)
    {
      _logger.LogError(
      "Unable to connect to IPC server after {Elapsed:N0} seconds. Shutting down.",
      elapsed.TotalSeconds);

      _appLifetime.Shutdown(1);
      return true;
    }
    return false;
  }
  private async Task CreateClientConnection(CancellationToken stoppingToken)
  {
    var pipeName = IpcPipeNames.GetPipeName(_desktopClientOptions.Value.InstanceId);

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        _logger.LogInformation("Attempting to connect to IPC server. Pipe Name: {PipeName}", pipeName);

        _client = await _ipcConnectionFactory.CreateClient(".", pipeName);

        await _client.Connect(stoppingToken);

        _logger.LogInformation("Connected to IPC server.");

        // Reset the connection attempt tracker on successful connection
        _firstFailedAttempt = null;

        _client.Start();
        _logger.LogInformation("Read started. Waiting for connection end.");
        await _client.WaitForDisconnect(stoppingToken);
        _client.Dispose();
        _client = null;
      }
      catch (OperationCanceledException ex)
      {
        _logger.LogInformation(ex, "App shutting down. Stopping IpcClientManager.");
        break;
      }
      catch (Exception ex)
      {
        _firstFailedAttempt ??= _timeProvider.GetUtcNow();
        _logger.LogError(ex, "Error while connecting to IPC server.");
      }

      try
      {
        if (CheckIfShouldShutdown())
        {
          return;
        }

        await Task.Delay(_reconnectDelay, _timeProvider, stoppingToken);
      }
      catch (OperationCanceledException)
      {
        _logger.LogInformation("App shutting down. Stopping IpcClientManager.");
        break;
      }
    }
  }
}