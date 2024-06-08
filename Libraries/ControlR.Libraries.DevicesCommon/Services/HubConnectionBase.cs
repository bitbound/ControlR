using Bitbound.SimpleMessenger;
using ControlR.Libraries.DevicesCommon.Messages;
using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Services;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.DevicesCommon.Services;

public interface IHubConnectionBase
{
    HubConnectionState ConnectionState { get; }
    bool IsConnected { get; }

    Task ReceiveDto(SignedPayloadDto dto);
}

public abstract class HubConnectionBase(
    IServiceScopeFactory _scopeFactory,
    IMessenger _messenger,
    IDelayer _delayer,
    ILogger<HubConnectionBase> _logger) : IHubConnectionBase
{
    protected readonly IDelayer _delayer = _delayer;
    protected readonly IMessenger _messenger = _messenger;
    private CancellationToken _cancellationToken;
    private HubConnection? _connection;
    private Func<string, Task> _onConnectFailure = reason => Task.CompletedTask;

    public HubConnectionState ConnectionState => _connection?.State ?? HubConnectionState.Disconnected;
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    protected HubConnection Connection => _connection ?? throw new Exception("You must start the connection first.");

    public Task ReceiveDto(SignedPayloadDto dto)
    {
        _messenger.Send(new SignedDtoReceivedMessage(dto));
        return Task.CompletedTask;
    }

    protected Task Connect(
        Func<string> hubUrlFactory,
        Action<HubConnection> connectionConfig,
        Action<HttpConnectionOptions> optionsConfig,
        CancellationToken cancellationToken)
    {
        return Connect(hubUrlFactory, connectionConfig, optionsConfig, _onConnectFailure, cancellationToken);
    }

    protected async Task Connect(
        Func<string> hubUrlFactory,
        Action<HubConnection> connectionConfig,
        Action<HttpConnectionOptions> optionsConfig,
        Func<string, Task> onConnectFailure,
        CancellationToken cancellationToken)
    {
        if (_connection is not null &&
            _connection.State != HubConnectionState.Disconnected)
        {
            return;
        }

        _cancellationToken = cancellationToken;
        _onConnectFailure = onConnectFailure;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var builder = scope.ServiceProvider.GetRequiredService<IHubConnectionBuilder>();

                if (_connection is not null)
                {
                    await _connection.DisposeAsync();
                }

                var hubUrl = hubUrlFactory();

                _connection = builder
                    .WithUrl(hubUrl, options =>
                    {
                        optionsConfig(options);
                    })
                    .AddMessagePackProtocol()
                    .WithStatefulReconnect()
                    .WithAutomaticReconnect(new RetryPolicy())
                    .Build();

                _connection.On<SignedPayloadDto>(nameof(ReceiveDto), ReceiveDto);
                _connection.Reconnecting += HubConnection_Reconnecting;
                _connection.Reconnected += HubConnection_Reconnected;
                _connection.Closed += HubConnection_Closed;

                connectionConfig.Invoke(_connection);

                _logger.LogInformation("Starting connection to {HubUrl}.", hubUrl);

                await _connection.StartAsync(cancellationToken);

                _logger.LogInformation("Connected to server.");

                break;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Failed to connect to server.  Status Code: {code}", ex.StatusCode);
                await _onConnectFailure.Invoke($"Communication failure.  Status Code: {ex.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in hub connection.");
                await _onConnectFailure.Invoke($"Connection error.  Message: {ex.Message}");
            }
            await Task.Delay(3_000, cancellationToken);
        }
    }

    protected async Task Reconnect(
        Func<string> hubUrlFactory,
        Action<HubConnection> connectionConfig,
        Action<HttpConnectionOptions> optionsConfig)
    {
        if (_connection is not null)
        {
            await _connection.StopAsync();
        }

        await Connect(hubUrlFactory, connectionConfig, optionsConfig, _onConnectFailure, _cancellationToken);
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
        await _delayer.WaitForAsync(() => IsConnected, TimeSpan.MaxValue);
    }

    private Task HubConnection_Closed(Exception? arg)
    {
        _logger.LogWarning(arg, "Hub connection closed.");
        return Task.CompletedTask;
    }

    private Task HubConnection_Reconnected(string? arg)
    {
        _logger.LogInformation("Reconnected to hub.  New connection ID: {id}", arg);
        return Task.CompletedTask;
    }

    private Task HubConnection_Reconnecting(Exception? arg)
    {
        _logger.LogInformation(arg, "Reconnecting to hub.");
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