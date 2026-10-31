using ControlR.Assist.Services;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs;
using ControlR.Libraries.Avalonia.Theming;
using ControlR.Libraries.Signalr.Client;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.Assist.Tests;

public class AssistSessionCoordinatorTests
{
  [Fact]
  public async Task EndSession_ReentrantServerNotification_CompletesWithoutDeadlock()
  {
    var hub = new FakeHubClient();
    var controller = CreateController(hub);

    Assert.True(await RunWithinTimeout(controller.ConnectAsync("server.example.com", CancellationToken.None)));
    hub.OnEndSession = sessionId =>
      controller.HandleSessionEnded(new(sessionId, AssistSessionEndedReason.EndedByPeer));

    await ActivateSessionAsync(controller, hub);

    await RunWithinTimeout(controller.EndSessionAsync());

    Assert.Equal(AssistSessionState.Ready, controller.State);
    Assert.False(string.IsNullOrEmpty(controller.Code));
    Assert.True(hub.EndSessionCallCount >= 1);
    Assert.True(hub.CreateSessionCallCount >= 2);

    await RunWithinTimeout(controller.DisposeAsync());
  }

  [Fact]
  public async Task HandleConsentCancelled_MatchingPendingSession_ClearsConsentAndGoesReady()
  {
    var hub = new FakeHubClient();
    var controller = CreateController(hub);

    Assert.True(await RunWithinTimeout(controller.ConnectAsync("server.example.com", CancellationToken.None)));
    var sessionId = hub.LastSessionId;
    await RunWithinTimeout(controller.HandleConsentRequested(new(sessionId, "Jane", "jane@example.com", Guid.NewGuid())));
    await WaitForStateAsync(controller, AssistSessionState.PendingConsent);

    await RunWithinTimeout(controller.HandleConsentCancelled(new(sessionId)));
    await WaitForStateAsync(controller, AssistSessionState.Ready);
    Assert.Null(controller.PendingSessionId);
    Assert.Equal(Localization.StatusConsentCancelled, controller.StatusMessage);
    await RunWithinTimeout(controller.DisposeAsync());
  }

  [Fact]
  public async Task HandleConsentCancelled_StaleSession_IsIgnored()
  {
    var hub = new FakeHubClient();
    var controller = CreateController(hub);

    Assert.True(await RunWithinTimeout(controller.ConnectAsync("server.example.com", CancellationToken.None)));
    await RunWithinTimeout(controller.HandleConsentRequested(new(hub.LastSessionId, "Jane", null, Guid.NewGuid())));
    await WaitForStateAsync(controller, AssistSessionState.PendingConsent);

    await RunWithinTimeout(controller.HandleConsentCancelled(new(Guid.NewGuid())));

    Assert.Equal(AssistSessionState.PendingConsent, controller.State);
    await RunWithinTimeout(controller.DisposeAsync());
  }

  [Fact]
  public async Task HandleConsentTimedOut_MatchingPendingSession_ClearsConsentAndGoesReady()
  {
    var hub = new FakeHubClient();
    var controller = CreateController(hub);

    Assert.True(await RunWithinTimeout(controller.ConnectAsync("server.example.com", CancellationToken.None)));
    var sessionId = hub.LastSessionId;
    await RunWithinTimeout(controller.HandleConsentRequested(new(sessionId, "Jane", "jane@example.com", Guid.NewGuid())));
    await WaitForStateAsync(controller, AssistSessionState.PendingConsent);

    await RunWithinTimeout(controller.HandleConsentTimedOut(new(sessionId)));
    await WaitForStateAsync(controller, AssistSessionState.Ready);
    Assert.Null(controller.PendingSessionId);
    Assert.Equal(Localization.StatusConsentTimedOut, controller.StatusMessage);
    await RunWithinTimeout(controller.DisposeAsync());
  }

  [Fact]
  public async Task HandleConsentTimedOut_StaleSession_IsIgnored()
  {
    var hub = new FakeHubClient();
    var controller = CreateController(hub);

    Assert.True(await RunWithinTimeout(controller.ConnectAsync("server.example.com", CancellationToken.None)));
    await RunWithinTimeout(controller.HandleConsentRequested(new(hub.LastSessionId, "Jane", null, Guid.NewGuid())));
    await WaitForStateAsync(controller, AssistSessionState.PendingConsent);

    await RunWithinTimeout(controller.HandleConsentTimedOut(new(Guid.NewGuid())));

    Assert.Equal(AssistSessionState.PendingConsent, controller.State);
    await RunWithinTimeout(controller.DisposeAsync());
  }

  [Theory]
  [InlineData(AssistSessionEndedReason.EndedByPeer, "The technician ended the session.")]
  [InlineData(AssistSessionEndedReason.PeerDisconnected, "The technician disconnected.")]
  [InlineData(AssistSessionEndedReason.AccessCodeExpired, "Your access code expired. Reconnect to get a new one.")]
  [InlineData(AssistSessionEndedReason.SessionExpired, "The support session expired.")]
  public async Task HandleSessionEnded_UsesTheLocalizedMessageForTheReason(
    AssistSessionEndedReason reason,
    string expectedMessage)
  {
    var hub = new FakeHubClient();
    var controller = CreateController(hub);

    Assert.True(await RunWithinTimeout(controller.ConnectAsync("server.example.com", CancellationToken.None)));
    await ActivateSessionAsync(controller, hub);
    await WaitForStateAsync(controller, AssistSessionState.Active);

    await RunWithinTimeout(controller.HandleSessionEnded(new(hub.LastSessionId, reason)));
    await WaitForStateAsync(controller, AssistSessionState.Ready);

    Assert.Equal(expectedMessage, controller.StatusMessage);
    await RunWithinTimeout(controller.DisposeAsync());
  }

  [Fact]
  public async Task HubInvokeThatNeverReturns_FailsWithStatusInsteadOfHanging()
  {
    var hub = new FakeHubClient
    {
      HangCreateSession = true,
    };
    var controller = CreateController(hub);

    var connected = await RunWithinTimeout(controller.ConnectAsync("server.example.com", CancellationToken.None));

    Assert.False(connected);
    Assert.Equal(AssistSessionState.Disconnected, controller.State);
    await RunWithinTimeout(controller.DisposeAsync());
  }

  [Fact]
  public async Task RespondToConsent_ForwardsThePendingConsentNonce()
  {
    var hub = new FakeHubClient();
    var controller = CreateController(hub);

    Assert.True(await RunWithinTimeout(controller.ConnectAsync("server.example.com", CancellationToken.None)));
    var nonce = Guid.NewGuid();
    await RunWithinTimeout(controller.HandleConsentRequested(new(hub.LastSessionId, "Jane", null, nonce)));
    await WaitForStateAsync(controller, AssistSessionState.PendingConsent);

    await RunWithinTimeout(controller.RespondToConsentAsync(true));

    Assert.Equal(nonce, hub.LastConsentNonce);
    await RunWithinTimeout(controller.DisposeAsync());
  }

  [Fact]
  public async Task RespondToConsent_SecondQueuedResponse_IsIgnored()
  {
    var hub = new FakeHubClient();
    var controller = CreateController(hub);

    Assert.True(await RunWithinTimeout(controller.ConnectAsync("server.example.com", CancellationToken.None)));
    await RunWithinTimeout(controller.HandleConsentRequested(new(hub.LastSessionId, "Jane", null, Guid.NewGuid())));
    await WaitForStateAsync(controller, AssistSessionState.PendingConsent);

    // Queue accept and decline back-to-back without awaiting, as double-clicking the consent
    // modal does. Only the first response may reach the server. A queued decline that lands
    // after the server already activated the session would tear the new session down.
    var accept = controller.RespondToConsentAsync(true);
    var decline = controller.RespondToConsentAsync(false);
    await RunWithinTimeout(accept);
    await RunWithinTimeout(decline);

    Assert.Equal(1, hub.ConsentResponseCallCount);
    Assert.True(hub.LastConsentApproved);
    Assert.Equal(AssistSessionState.PendingConsent, controller.State);
    await RunWithinTimeout(controller.DisposeAsync());
  }

  private static async Task ActivateSessionAsync(AssistSessionCoordinator controller, FakeHubClient hub)
  {
    await RunWithinTimeout(controller.HandleSessionStarted(
      new(hub.LastSessionId, "token", "Jane", "jane@example.com")));

    await WaitForStateAsync(controller, AssistSessionState.Active);
  }

  private static AssistSessionCoordinator CreateController(FakeHubClient hub) =>
    new(
      hub,
      new FakeHostFactory(),
      new FakeSettingsService(),
      NullLogger<AssistSessionCoordinator>.Instance);

  private static async Task RunWithinTimeout(Task task)
  {
    var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30)));
    Assert.True(ReferenceEquals(completed, task), "The operation did not complete within the timeout.");
    await task;
  }

  private static async Task<T> RunWithinTimeout<T>(Task<T> task)
  {
    var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30)));
    Assert.True(ReferenceEquals(completed, task), "The operation did not complete within the timeout.");
    return await task;
  }

  private static async Task RunWithinTimeout(ValueTask valueTask) => await RunWithinTimeout(valueTask.AsTask());

  private static async Task WaitForStateAsync(AssistSessionCoordinator controller, AssistSessionState expected)
  {
    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
    while (controller.State != expected && DateTime.UtcNow < deadline)
    {
      await Task.Delay(25);
    }

    Assert.Equal(expected, controller.State);
  }

  private sealed class FakeHostFactory : IAssistSessionHostFactory
  {
    public IHost CreateSessionHost(AssistSessionRequest request) => Host.CreateApplicationBuilder().Build();
  }
  private sealed class FakeHubClient : IHubConnection<IAssistHub>, IAssistHub
  {
    private Guid _lastSessionId = Guid.NewGuid();

    event Func<Exception?, Task>? IHubConnection<IAssistHub>.Closed { add { } remove { } }
    event Func<Exception, Task>? IHubConnection<IAssistHub>.ConnectThrew { add { } remove { } }
    event Func<string?, Task>? IHubConnection<IAssistHub>.Reconnected { add { } remove { } }
    event Func<Exception?, Task>? IHubConnection<IAssistHub>.Reconnecting { add { } remove { } }

    public string? ConnectionId => "test-connection";
    public HubConnectionState ConnectionState => HubConnectionState.Connected;
    public int ConsentResponseCallCount { get; private set; }
    public int CreateSessionCallCount { get; private set; }
    public int EndSessionCallCount { get; private set; }
    public bool HangCreateSession { get; init; }
    public bool IsConnected => true;
    public bool LastConsentApproved { get; private set; }
    public Guid LastConsentNonce { get; private set; }
    public Guid LastSessionId => _lastSessionId;
    public Action<Guid>? OnEndSession { get; set; }
    public IAssistHub Server => this;

    public Task<HubResult> CancelAssistConsent(CancelAssistConsentRequestDto request) =>
      Task.FromResult(HubResult.Ok());

    public Task<bool> Connect(
      Uri hubEndpoint,
      bool autoRetry,
      Action<HttpConnectionOptions>? configure = null,
      CancellationToken cancellationToken = default) => Task.FromResult(true);

    public async Task<HubResult<CreateAssistSessionResponseDto>> CreateAssistSession()
    {
      CreateSessionCallCount++;

      if (HangCreateSession)
      {
        // Simulates an unresponsive server. The coordinator's own invoke timeout must unstick it.
        await Task.Delay(Timeout.Infinite);
      }

      _lastSessionId = Guid.NewGuid();
      return HubResult.Ok(new CreateAssistSessionResponseDto("123456789", _lastSessionId));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<HubResult> EndAssistSession(EndAssistSessionRequestDto request)
    {
      EndSessionCallCount++;

      // Reproduces the end-session race. The server-side ended notification arrives while the
      // end call is still in flight. Handlers must not wedge the coordinator.
      OnEndSession?.Invoke(request.SessionId);
      return Task.FromResult(HubResult.Ok());
    }

    public Task<HubResult<JoinAssistSessionResponseDto>> JoinAssistSession(JoinAssistSessionRequestDto request) =>
      Task.FromResult(HubResult.Fail<JoinAssistSessionResponseDto>("The assist app never joins a session."));

    public Task<HubResult> RespondToAssistConsent(RespondToAssistConsentRequestDto request)
    {
      ConsentResponseCallCount++;
      LastConsentApproved = request.Approved;
      LastConsentNonce = request.ConsentNonce;
      return Task.FromResult(HubResult.Ok());
    }

    public Task Send(string methodName, object?[] args, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;

    public Task WaitForConnected(CancellationToken cancellationToken) => Task.CompletedTask;
  }

  private sealed class FakeSettingsService : IAssistSettingsService
  {
    public string? GetLanguage() => null;

    public AssistAppSettings GetSettings() => new();

    public AvaloniaThemeMode GetThemeMode() => AvaloniaThemeMode.System;

    public Task SaveAsync(AssistAppSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;

    public void SetLanguage(string? language)
    {
    }

    public void SetThemeMode(AvaloniaThemeMode mode)
    {
    }
  }
}
