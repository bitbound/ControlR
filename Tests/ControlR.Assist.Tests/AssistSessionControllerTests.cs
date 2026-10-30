using ControlR.Assist.Services;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.Assist.Tests;

public class AssistSessionControllerTests
{
  [Fact]
  public async Task EndSession_ReentrantServerNotification_CompletesWithoutDeadlock()
  {
    var hub = new FakeHubClient();
    var controller = CreateController(hub);

    Assert.True(await RunWithinTimeout(controller.ConnectAsync("server.example.com", CancellationToken.None)));
    hub.OnEndSession = sessionId =>
      controller.HandleSessionEnded(new(sessionId, "The session was ended by the remote user."));

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
    // modal does. Only the first response may reach the server; a queued decline that lands
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

  private static async Task ActivateSessionAsync(AssistSessionController controller, FakeHubClient hub)
  {
    await RunWithinTimeout(controller.HandleSessionStarted(
      new(hub.LastSessionId, "token", "Jane", "jane@example.com")));

    await WaitForStateAsync(controller, AssistSessionState.Active);
  }

  private static AssistSessionController CreateController(FakeHubClient hub) =>
    new(
      hub,
      new FakeHostFactory(),
      new FakeSettingsService(),
      NullLogger<AssistSessionController>.Instance);

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

  private static async Task WaitForStateAsync(AssistSessionController controller, AssistSessionState expected)
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
  private sealed class FakeHubClient : IAssistSignalRClient
  {
    private Guid _lastSessionId = Guid.NewGuid();

    public int ConsentResponseCallCount { get; private set; }
    public int CreateSessionCallCount { get; private set; }
    public int EndSessionCallCount { get; private set; }
    public bool HangCreateSession { get; init; }
    public bool IsConnected => true;
    public bool LastConsentApproved { get; private set; }
    public Guid LastConsentNonce { get; private set; }
    public Guid LastSessionId => _lastSessionId;

    public Func<Task>? OnConnectionLost { get; set; }
    public Func<AssistConsentCancelledDto, Task>? OnConsentCancelled { get; set; }
    public Func<AssistConsentRequestedDto, Task>? OnConsentRequested { get; set; }
    public Func<AssistConsentTimedOutDto, Task>? OnConsentTimedOut { get; set; }
    public Action<Guid>? OnEndSession { get; set; }
    public Func<Task>? OnReconnected { get; set; }
    public Func<AssistSessionEndedDto, Task>? OnSessionEnded { get; set; }
    public Func<AssistSessionStartedDto, Task>? OnSessionStarted { get; set; }

    public Task<bool> ConnectAsync(Uri serverBaseUri, CancellationToken cancellationToken) => Task.FromResult(true);

    public async Task<HubResult<CreateAssistSessionResponseDto>> CreateAssistSession(CancellationToken cancellationToken)
    {
      CreateSessionCallCount++;

      if (HangCreateSession)
      {
        // Simulates an unresponsive server. The controller's own invoke timeout must unstick it.
        try
        {
          await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }

        return HubResult.Fail<CreateAssistSessionResponseDto>("The assist client is not connected.");
      }

      _lastSessionId = Guid.NewGuid();
      return HubResult.Ok(new CreateAssistSessionResponseDto("123456789", _lastSessionId));
    }

    public Task DisconnectAsync() => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<HubResult> EndAssistSession(Guid sessionId, CancellationToken cancellationToken)
    {
      EndSessionCallCount++;

      // Reproduces the end-session race: the server-side ended notification arrives while the
      // end call is still in flight. Handlers must not wedge the controller.
      OnEndSession?.Invoke(sessionId);
      return Task.FromResult(HubResult.Ok());
    }

    public Task<HubResult> RespondToAssistConsent(
      Guid sessionId,
      bool approved,
      Guid consentNonce,
      CancellationToken cancellationToken)
    {
      ConsentResponseCallCount++;
      LastConsentApproved = approved;
      LastConsentNonce = consentNonce;
      return Task.FromResult(HubResult.Ok());
    }
  }
  private sealed class FakeSettingsService : IAssistSettingsService
  {
    public AssistAppSettings GetSettings() => new();

    public Task SaveAsync(AssistAppSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;
  }
}
