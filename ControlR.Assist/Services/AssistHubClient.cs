using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs;
using ControlR.Libraries.Shared.Constants;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using AssistHubClientContract = ControlR.Libraries.Api.Contracts.Hubs.Clients.IAssistHubClient;

namespace ControlR.Assist.Services;

/// <summary>
/// Talks to the assist control plane. Uses the raw SignalR client rather than the strongly typed
/// hub connection because Assist ships trimmed and the typed proxy emits dynamic assemblies at
/// runtime, which does not survive trimming.
/// </summary>
public interface IAssistSignalRClient : IAsyncDisposable
{
  bool IsConnected { get; }
  Func<AssistConsentRequestedDto, Task>? OnConsentRequested { get; set; }
  Func<Task>? OnReconnected { get; set; }
  Func<AssistSessionEndedDto, Task>? OnSessionEnded { get; set; }
  Func<AssistSessionStartedDto, Task>? OnSessionStarted { get; set; }

  Task<bool> ConnectAsync(Uri serverBaseUri, CancellationToken cancellationToken);
  Task<HubResult<CreateAssistSessionResponseDto>> CreateAssistSession(CancellationToken cancellationToken);
  Task DisconnectAsync();
  Task<HubResult> EndAssistSession(Guid sessionId, CancellationToken cancellationToken);
  Task<HubResult> RespondToAssistConsent(Guid sessionId, bool approved, CancellationToken cancellationToken);
}

public sealed class AssistHubClient(ILogger<AssistHubClient> logger) : IAssistSignalRClient
{
  private static readonly TimeSpan[] _reconnectDelays =
  [
    TimeSpan.Zero,
    TimeSpan.FromSeconds(2),
    TimeSpan.FromSeconds(5),
    TimeSpan.FromSeconds(10),
    TimeSpan.FromSeconds(30)
  ];

  private readonly ILogger<AssistHubClient> _logger = logger;

  private HubConnection? _connection;

  public bool IsConnected => _connection?.State == HubConnectionState.Connected;
  public Func<AssistConsentRequestedDto, Task>? OnConsentRequested { get; set; }
  public Func<Task>? OnReconnected { get; set; }
  public Func<AssistSessionEndedDto, Task>? OnSessionEnded { get; set; }
  public Func<AssistSessionStartedDto, Task>? OnSessionStarted { get; set; }

  public async Task<bool> ConnectAsync(Uri serverBaseUri, CancellationToken cancellationToken)
  {
    await DisconnectAsync();

    var hubEndpoint = new Uri(serverBaseUri, AppConstants.AssistHubPath);

    // The control plane also accepts JSON, so this stays on the default protocol to avoid the
    // MessagePack reflection path, which the linker cannot trace.
    var connection = new HubConnectionBuilder()
      .WithUrl(hubEndpoint)
      .WithAutomaticReconnect(_reconnectDelays)
      .Build();

    connection.Reconnected += _ => InvokeHandler(OnReconnected);
    connection.Closed += ex =>
    {
      if (ex is not null)
      {
        _logger.LogWarning(ex, "Assist hub connection closed.");
      }

      return Task.CompletedTask;
    };

    connection.On(
      nameof(AssistHubClientContract.AssistConsentRequested),
      (AssistConsentRequestedDto dto) => InvokeHandler(OnConsentRequested, dto));
    connection.On(
      nameof(AssistHubClientContract.AssistSessionEnded),
      (AssistSessionEndedDto dto) => InvokeHandler(OnSessionEnded, dto));
    connection.On(
      nameof(AssistHubClientContract.AssistSessionStarted),
      (AssistSessionStartedDto dto) => InvokeHandler(OnSessionStarted, dto));

    try
    {
      await connection.StartAsync(cancellationToken);
      _connection = connection;
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to connect to the assist hub at {Endpoint}.", hubEndpoint);
      await DisposeConnection(connection);
      return false;
    }
  }

  public Task<HubResult<CreateAssistSessionResponseDto>> CreateAssistSession(CancellationToken cancellationToken) =>
    InvokeAsync(
      nameof(IAssistHub.CreateAssistSession),
      [],
      () => HubResult.Fail<CreateAssistSessionResponseDto>("The assist client is not connected."),
      cancellationToken);

  public async Task DisconnectAsync()
  {
    var connection = Interlocked.Exchange(ref _connection, null);
    if (connection is not null)
    {
      await DisposeConnection(connection);
    }
  }

  public async ValueTask DisposeAsync() => await DisconnectAsync();

  public Task<HubResult> EndAssistSession(Guid sessionId, CancellationToken cancellationToken) =>
    InvokeAsync(
      nameof(IAssistHub.EndAssistSession),
      [new EndAssistSessionRequestDto(sessionId)],
      () => HubResult.Fail("The assist client is not connected."),
      cancellationToken);

  public Task<HubResult> RespondToAssistConsent(Guid sessionId, bool approved, CancellationToken cancellationToken) =>
    InvokeAsync(
      nameof(IAssistHub.RespondToAssistConsent),
      [new RespondToAssistConsentRequestDto(sessionId, approved)],
      () => HubResult.Fail("The assist client is not connected."),
      cancellationToken);

  private static async Task InvokeHandler(Func<Task>? handler)
  {
    if (handler is not null)
    {
      await handler();
    }
  }

  private static async Task InvokeHandler<TDto>(Func<TDto, Task>? handler, TDto dto)
  {
    if (handler is not null)
    {
      await handler(dto);
    }
  }

  private async Task DisposeConnection(HubConnection connection)
  {
    try
    {
      await connection.StopAsync(CancellationToken.None);
      await connection.DisposeAsync();
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Error while disposing the assist hub connection.");
    }
  }

  private async Task<TResult> InvokeAsync<TResult>(
    string methodName,
    object?[] args,
    Func<TResult> fallback,
    CancellationToken cancellationToken)
  {
    if (_connection is not { State: HubConnectionState.Connected } connection)
    {
      return fallback();
    }

    try
    {
      return await connection.InvokeAsync<TResult>(methodName, args, cancellationToken);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while invoking assist hub method {MethodName}.", methodName);
      return fallback();
    }
  }
}
