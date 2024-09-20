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
  IServiceProvider services,
  IMessenger messenger,
  IDelayer delayer,
  ILogger<HubConnectionBase> logger) : IHubConnectionBase
{
  protected readonly IDelayer Delayer = delayer;
  protected readonly IMessenger Messenger = messenger;
  protected readonly IServiceProvider Services = services;
  private CancellationToken _cancellationToken;
  private HubConnection? _connection;
  private Func<string, Task> _onConnectFailure = _ => Task.CompletedTask;
  private bool _useReconnect;

  protected HubConnection Connection => _connection ?? throw new Exception("You must start the connection first.");

  public HubConnectionState ConnectionState => _connection?.State ?? HubConnectionState.Disconnected;
  public bool IsConnected => _connection?.State == HubConnectionState.Connected;

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
    _cancellationToken = cancellationToken;
    _onConnectFailure = onConnectFailure;

    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        using var scope = Services.CreateScope();
        var builder = scope.ServiceProvider.GetRequiredService<IHubConnectionBuilder>();

        if (_connection is not null)
        {
          await _connection.DisposeAsync();
        }

        var hubUrl = hubUrlFactory();

        builder = builder
          .WithUrl(hubUrl, options => { optionsConfig(options); })
          .AddMessagePackProtocol();

        if (useReconnect)
        {
          builder
            .WithStatefulReconnect()
            .WithAutomaticReconnect(new RetryPolicy());
        }

        _connection = builder.Build();

        _connection.On<DtoWrapper>(nameof(ReceiveDto), ReceiveDto);
        _connection.Reconnecting += HubConnection_Reconnecting;
        _connection.Reconnected += HubConnection_Reconnected;
        _connection.Closed += HubConnection_Closed;

        connectionConfig.Invoke(_connection);

        logger.LogInformation("Starting connection to {HubUrl}.", hubUrl);

        await _connection.StartAsync(cancellationToken);

        logger.LogInformation("Connected to server.");

        break;
      }
      catch (HttpRequestException ex)
      {
        logger.LogWarning(ex, "Failed to connect to server.  Status Code: {code}", ex.StatusCode);
        await _onConnectFailure.Invoke($"Communication failure.  Status Code: {ex.StatusCode}");
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error in hub connection.");
        await _onConnectFailure.Invoke($"Connection error.  Message: {ex.Message}");
      }

      await Task.Delay(3_000, cancellationToken);
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

    await Connect(hubUrlFactory, connectionConfig, optionsConfig, _onConnectFailure, _useReconnect, _cancellationToken);
  }

  protected async Task StopConnection(CancellationToken cancellationToken)
  {
    if (_connection is not null)
    {
      await _connection.StopAsync(cancellationToken);
    }
  }

  protected async Task WaitForConnection()
  {
    await Delayer.WaitForAsync(() => IsConnected, TimeSpan.MaxValue);
  }

  private Task HubConnection_Closed(Exception? arg)
  {
    logger.LogWarning(arg, "Hub connection closed.");
    return Task.CompletedTask;
  }

  private Task HubConnection_Reconnected(string? arg)
  {
    logger.LogInformation("Reconnected to hub.  New connection ID: {id}", arg);
    return Task.CompletedTask;
  }

  private Task HubConnection_Reconnecting(Exception? arg)
  {
    logger.LogInformation(arg, "Reconnecting to hub.");
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