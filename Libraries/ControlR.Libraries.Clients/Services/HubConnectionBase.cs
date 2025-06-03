using ControlR.Libraries.Shared.Services;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Libraries.Clients.Services;

public interface IHubConnectionBase
{
  HubConnectionState ConnectionState { get; }
  bool IsConnected { get; }

  Task ReceiveDto(DtoWrapper dtoWrapper);
}

public abstract class HubConnectionBase(
  TimeProvider timeProvider,
  IServiceProvider services,
  IMessenger messenger,
  ILogger<HubConnectionBase> logger) : IHubConnectionBase
{
  protected readonly ILogger<HubConnectionBase> Logger = logger;
  protected readonly IMessenger Messenger = messenger;
  protected readonly IServiceProvider Services = services;
  protected readonly TimeProvider TimeProvider = timeProvider;

  private readonly ManualResetEventAsync _connectionReady = new(false);
  private HubConnection? _connection;
  private Func<string, Task> _onConnectFailure = _ => Task.CompletedTask;
  private bool _useReconnect;

  public HubConnectionState ConnectionState => _connection?.State ?? HubConnectionState.Disconnected;
  public bool IsConnected => _connection?.State == HubConnectionState.Connected;
  protected HubConnection Connection => _connection ?? throw new InvalidOperationException("You must start the connection first.");
  public Task ReceiveDto(DtoWrapper dto)
  {
    Messenger.Send(new DtoReceivedMessage<DtoWrapper>(dto)).Forget();
    return Task.CompletedTask;
  }

  protected Task Connect(
    Func<Uri> hubUrlFactory,
    Action<HubConnection> connectionConfig,
    Action<HttpConnectionOptions> optionsConfig,
    bool useReconnect,
    CancellationToken cancellationToken)
  {
    return Connect(hubUrlFactory, connectionConfig, optionsConfig, _onConnectFailure, useReconnect, cancellationToken);
  }

  protected async Task Connect(
    Func<Uri> hubUrlFactory,
    Action<HubConnection> connectionConfig,
    Action<HttpConnectionOptions> optionsConfig,
    Func<string, Task> onConnectFailure,
    bool useReconnect,
    CancellationToken cancellationToken)
  {
    if (_connection is not null &&
        _connection.State != HubConnectionState.Disconnected)
    {
      return;
    }

    _useReconnect = useReconnect;
    _onConnectFailure = onConnectFailure;

    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        var builder = Services.GetRequiredService<IHubConnectionBuilder>();

        if (_connection is not null)
        {
          await _connection.DisposeAsync();
        }

        var hubUrl = hubUrlFactory();

        builder = builder.WithUrl(hubUrl, optionsConfig);

        if (useReconnect)
        {
          builder.WithAutomaticReconnect(new RetryPolicy());
        }

        _connection = builder.Build();

        _connection.On<DtoWrapper>(nameof(ReceiveDto), ReceiveDto);
        _connection.Reconnecting += HubConnection_Reconnecting;
        _connection.Reconnected += HubConnection_Reconnected;
        _connection.Closed += HubConnection_Closed;

        connectionConfig.Invoke(_connection);

        Logger.LogInformation("Starting connection to {HubUrl}.", hubUrl);

        await _connection.StartAsync(cancellationToken);

        Logger.LogInformation("Connected to server.");

        break;
      }
      catch (HttpRequestException ex)
      {
        Logger.LogWarning(ex, "Failed to connect to server.  Status Code: {code}", ex.StatusCode);
        await _onConnectFailure.Invoke($"Communication failure.  Status Code: {ex.StatusCode}");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Error in hub connection.");
        await _onConnectFailure.Invoke($"Connection error.  Message: {ex.Message}");
      }

      await Task.Delay(TimeSpan.FromSeconds(3), TimeProvider, cancellationToken);
    }
  }

  protected async Task Reconnect(
    Func<Uri> hubUrlFactory,
    Action<HubConnection> connectionConfig,
    Action<HttpConnectionOptions> optionsConfig)
  {
    if (_connection is not null)
    {
      await _connection.StopAsync();
    }

    await Connect(hubUrlFactory, connectionConfig, optionsConfig, _onConnectFailure, _useReconnect, CancellationToken.None);
  }

  protected async Task StopConnection(CancellationToken cancellationToken)
  {
    if (_connection is not null)
    {
      await _connection.StopAsync(cancellationToken);
      _connectionReady.Reset();
    }
  }

  protected async Task WaitForConnection(CancellationToken cancellationToken)
  {
    await _connectionReady.Wait(cancellationToken);
  }

  private Task HubConnection_Closed(Exception? arg)
  {
    Logger.LogWarning(arg, "Hub connection closed.");
    _connectionReady.Reset();
    return Task.CompletedTask;
  }

  private Task HubConnection_Reconnected(string? arg)
  {
    Logger.LogInformation("Reconnected to hub.  New connection ID: {id}", arg);
    _connectionReady.Set();
    return Task.CompletedTask;
  }

  private Task HubConnection_Reconnecting(Exception? arg)
  {
    Logger.LogInformation(arg, "Reconnecting to hub.");
    _connectionReady.Reset();
    return Task.CompletedTask;
  }

  private class RetryPolicy : IRetryPolicy
  {
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
      return TimeSpan.FromSeconds(3);
    }
  }
}