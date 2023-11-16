using Bitbound.SimpleMessenger;
using ControlR.Devices.Common.Messages;
using ControlR.Shared.Dtos;
using ControlR.Shared.Helpers;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlR.Devices.Common.Services;

public interface IHubConnectionBase
{
    HubConnectionState ConnectionState { get; }
    bool IsConnected { get; }

    Task ReceiveDto(SignedPayloadDto dto);

    Task StopConnection(CancellationToken cancellationToken);
}

public abstract class HubConnectionBase(
    IServiceScopeFactory _scopeFactory,
    IMessenger messenger,
    ILogger<HubConnectionBase> _logger) : IHubConnectionBase
{
    protected readonly IMessenger _messenger = messenger;
    private CancellationToken _cancellationToken;
    private HubConnection? _connection;
    private Func<string, Task> _onConnectFailure = reason => Task.CompletedTask;

    public HubConnectionState ConnectionState => _connection?.State ?? HubConnectionState.Disconnected;
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    protected HubConnection Connection => _connection ?? throw new Exception("You must start the connection first.");

    public Task Connect(
        string hubUrl,
        Action<HubConnection> connectionConfig,
        Action<HttpConnectionOptions> optionsConfig,
        CancellationToken cancellationToken)
    {
        return Connect(hubUrl, connectionConfig, optionsConfig, _onConnectFailure, cancellationToken);
    }

    public async Task Connect(
        string hubUrl,
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

                _connection = builder
                    .WithUrl(hubUrl, options =>
                    {
                        optionsConfig(options);
                    })
                    .AddMessagePackProtocol()
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

    public Task ReceiveDto(SignedPayloadDto dto)
    {
        _messenger.Send(new SignedDtoReceivedMessage(dto));
        return Task.CompletedTask;
    }

    public async Task Reconnect(string hubUrl,
        Action<HubConnection> connectionConfig,
        Action<HttpConnectionOptions> optionsConfig)
    {
        if (_connection is not null)
        {
            await _connection.StopAsync();
        }

        await Connect(hubUrl, connectionConfig, optionsConfig, _onConnectFailure, _cancellationToken);
    }

    public async Task StopConnection(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            await _connection.StopAsync(cancellationToken);
        }
    }

    protected async Task WaitForConnection()
    {
        await WaitHelper.WaitForAsync(() => IsConnected, TimeSpan.MaxValue);
    }

    private Task HubConnection_Closed(Exception? arg)
    {
        _logger.LogWarning(arg, "Hub connection closed.");
        return Task.CompletedTask;
    }

    private Task HubConnection_Reconnected(string? arg)
    {
        _logger.LogInformation("Reconnected to desktop hub.  New connection ID: {id}", arg);
        return Task.CompletedTask;
    }

    private Task HubConnection_Reconnecting(Exception? arg)
    {
        _logger.LogInformation(arg, "Reconnecting to desktop hub.");
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