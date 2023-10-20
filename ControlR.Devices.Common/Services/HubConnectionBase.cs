using ControlR.Shared.Dtos;
using ControlR.Shared.Helpers;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlR.Devices.Common.Services;
public interface IHubConnectionBase
{
    event EventHandler<SignedPayloadDto>? DtoReceived;
    HubConnectionState ConnectionState { get; }
    bool IsConnected { get; }

    Task ReceiveDto(SignedPayloadDto dto);
    Task Stop(CancellationToken cancellationToken);
}

public abstract class HubConnectionBase(
    IServiceScopeFactory scopeFactory,
    ILogger<HubConnectionBase> logger) : IHubConnectionBase
{
    protected readonly ILogger<HubConnectionBase> _logger = logger;
    protected readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private CancellationToken _cancellationToken;
    private Func<string, Task> _onConnectFailure = reason => Task.CompletedTask;
    private HubConnection? _connection;

    public event EventHandler<SignedPayloadDto>? DtoReceived;

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

        using var scope = _scopeFactory.CreateScope();
        var builder = scope.ServiceProvider.GetRequiredService<IHubConnectionBuilder>();

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
        await StartConnection();
    }

    public Task ReceiveDto(SignedPayloadDto dto)
    {
        DtoReceived?.Invoke(this, dto);
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

    public async Task Stop(CancellationToken cancellationToken)
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

    private async Task StartConnection()
    {
        if (_connection is null)
        {
            _logger.LogWarning("Connection shouldn't be null here.");
            return;
        }

        while (!_cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Connecting to server.");

                await _connection.StartAsync(_cancellationToken);

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
            await Task.Delay(3_000, _cancellationToken);
        }
    }
    private class RetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            return TimeSpan.FromSeconds(3);
        }
    }
}
