using System.Net;
using System.Text.Json;
using ControlR.ApiClient.Auth;
using ControlR.ApiClient.Internal;
using ControlR.ApiClient.Tests.Helpers;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Waiter = ControlR.Libraries.Shared.Services.Waiter;

namespace ControlR.ApiClient.Tests;

public sealed class ControlrAuthSessionTests
{
  private static readonly Uri _server = new("https://server.test/");

  [Fact]
  public async Task Dispose_WhenAuthenticated_RaisesDisposedAndClearsIsAuthenticated()
  {
    var handler = new RecordingHttpMessageHandler(TokenIssuingResponder());
    using var session = CreateSession(handler, new FakeTimeProvider());
    var raised = new List<ControlrAuthSessionState>();
    session.StateChanged += (_, args) => raised.Add(args.State);

    var login = await session.SignIn(
      new InteractiveSignInRequest { Email = "u@test.test", Password = "pw" },
      TestContext.Current.CancellationToken);
    Assert.Equal(InteractiveLoginStatus.Authenticated, login.Status);

    session.Dispose();

    // A caller still holding this reference used to see Authenticated forever with no event, because
    // disposal only stopped the refresh loop and never touched the state.
    Assert.Equal(ControlrAuthSessionState.Disposed, session.State);
    Assert.False(session.IsAuthenticated);
    Assert.Contains(ControlrAuthSessionState.Disposed, raised);
  }

  [Fact]
  public async Task Dispose_WhenCalledTwice_RaisesTheChangeOnce()
  {
    var handler = new RecordingHttpMessageHandler(TokenIssuingResponder());
    var session = CreateSession(handler, new FakeTimeProvider());
    var raised = new List<ControlrAuthSessionState>();
    session.StateChanged += (_, args) => raised.Add(args.State);

    await session.SignIn(
      new InteractiveSignInRequest { Email = "u@test.test", Password = "pw" },
      TestContext.Current.CancellationToken);

    session.Dispose();
    session.Dispose();

    Assert.Equal(1, raised.Count(state => state == ControlrAuthSessionState.Disposed));
  }

  [Fact]
  public void Dispose_WhenSignedOut_TransitionsToDisposedWithoutRaisingTheChange()
  {
    using var session = CreateSession(new RecordingHttpMessageHandler(), new FakeTimeProvider());
    var raised = new List<ControlrAuthSessionState>();
    session.StateChanged += (_, args) => raised.Add(args.State);

    session.Dispose();

    // SignedOut already tells an observer the session cannot authenticate, and this is the state a
    // never-used session is disposed in during host shutdown, where an extra event is only noise.
    Assert.Equal(ControlrAuthSessionState.Disposed, session.State);
    Assert.Empty(raised);
  }

  [Fact]
  public async Task Dispose_WhenSignInCompletesAfterwards_KeepsTheTerminalState()
  {
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var handler = new GatedLoginHandler(gate.Task);
    var session = CreateSession(handler, new FakeTimeProvider());

    var signIn = session.SignIn(
      new InteractiveSignInRequest { Email = "u@test.test", Password = "pw" },
      TestContext.Current.CancellationToken);

    Assert.True(await Waiter.Default.WaitFor(() => handler.LoginStarted, cancellationToken: cts.Token));

    session.Dispose();
    gate.SetResult();

    var result = await signIn;

    // The server accepted the login while the session was being disposed. The result stays truthful
    // about the server, and the state stays truthful about the session. Reporting Authenticated here
    // would resurrect a terminal state on an object with no transport left.
    Assert.Equal(InteractiveLoginStatus.Authenticated, result.Status);
    Assert.Equal(ControlrAuthSessionState.Disposed, session.State);
    Assert.False(session.IsAuthenticated);
  }

  [Fact]
  public async Task FactoryEviction_WhileRefreshInFlight_DoesNotThrowObjectDisposed()
  {
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var handler = new GatedRefreshHandler(gate.Task);
    var loggerFactory = new RecordingLoggerFactory();
    var timeProvider = new FakeTimeProvider();

    var factoryOptions = new ControlrApiClientFactoryOptions
    {
      MaxIdleClientLifetime = null,
      HttpMessageHandlerFactory = () => handler
    };
    var factory = new ControlrApiClientFactory(
      factoryOptions,
      timeProvider,
      loggerFactory);

    factory.GetOrCreateClient("a", o => o.BaseUrl = _server);

    var session = factory.GetOrCreateAuthSession("a");
    await session.RestoreAuthSnapshot(ExpiredSnapshot("access-old", "refresh-old"));

    // Wait until the refresh request is actually in flight (semaphore held). Use a real-time
    // waiter so the poll delay isn't tied to the fake clock driving the session.
    var waitResult = await Waiter.Default.WaitFor(() => handler.RefreshStarted, cancellationToken: cts.Token);
    Assert.True(waitResult);

    // Evict mid-flight: the entry teardown disposes the bearer-refresh lock while the
    // refresher's finally-block Release() is still pending.
    Assert.True(factory.TryRemoveClient("a"));
    gate.SetResult();

    // Give the in-flight completion time to run its finally block and the loop to react.
    await Task.Delay(TimeSpan.FromMilliseconds(250), cts.Token);

    // Pre-fix, the ObjectDisposedException from the finally block escaped into the refresh
    // loop, which logged "refresh failed" and retried. The fixed path completes the refresh
    // quietly against the discarded entry, so no failure may be logged.
    Assert.DoesNotContain(loggerFactory.Messages, m => m.Contains("failed", StringComparison.OrdinalIgnoreCase));

    // The factory must remain usable after the mid-flight eviction.
    factory.GetOrCreateClient("b", o => o.BaseUrl = _server);
    Assert.Equal(["b"], factory.GetClientNames());
    factory.Dispose();
  }

  [Fact]
  public async Task RestoreAuthSnapshot_AfterProcessRestart_ResumesWithoutAnyLoginRequest()
  {
    var handler = new RecordingHttpMessageHandler(TokenIssuingResponder());
    var timeProvider = new FakeTimeProvider();
    var original = CreateSession(handler, timeProvider);

    var login = await original.SignIn(
      new InteractiveSignInRequest { Email = "u@test.test", Password = "pw" },
      TestContext.Current.CancellationToken);
    Assert.Equal(InteractiveLoginStatus.Authenticated, login.Status);

    var snapshot = original.GetAuthSnapshot();
    Assert.Equal("access-1", snapshot.BearerToken);
    Assert.Equal("refresh-1", snapshot.RefreshToken);
    original.Dispose();

    // Simulate the host app restart process-wide: brand-new session, restored snapshot, and a
    // handler that records everything the new process does.
    var restartHandler = new RecordingHttpMessageHandler(TokenIssuingResponder());
    var restored = CreateSession(restartHandler, timeProvider);
    await restored.RestoreAuthSnapshot(snapshot);

    Assert.Equal(ControlrAuthSessionState.Authenticated, restored.State);
    var token = await restored.GetBearerToken(TestContext.Current.CancellationToken);
    Assert.Equal("access-1", token);

    // No interaction with the login endpoint at all means the server never challenged for 2FA,
    // which is the OTP-prompt guarantee the host is buying with snapshot persistence.
    Assert.DoesNotContain(
      restartHandler.Requests,
      r => r.RequestUri!.AbsolutePath.EndsWith("/interactive-login", StringComparison.Ordinal));
    restored.Dispose();
  }

  [Fact]
  public async Task RestoreAuthSnapshot_WhenBearerExpired_RefreshesInBackgroundWithoutLogin()
  {
    var timeProvider = new FakeTimeProvider();
    var snapshot = ExpiredSnapshot(bearer: "access-old", refresh: "refresh-old");

    var handler = new RecordingHttpMessageHandler(RefreshOnlyResponder());
    var session = CreateSession(handler, timeProvider);
    await session.RestoreAuthSnapshot(snapshot);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var waitResult = await Waiter.Default.WaitFor(
      () => Equal(session.GetAuthSnapshot().BearerToken, "access-renewed"),
      cancellationToken: cts.Token);
    Assert.True(waitResult);

    Assert.Equal(ControlrAuthSessionState.Authenticated, session.State);
    Assert.DoesNotContain(
      handler.Requests,
      r => r.RequestUri!.AbsolutePath.EndsWith("/interactive-login", StringComparison.Ordinal));
    var refresh = Assert.Single(
      handler.Requests,
      r => r.RequestUri!.AbsolutePath.EndsWith("/api/auth/refresh", StringComparison.Ordinal));
    Assert.Equal(new Uri(_server, "/api/auth/refresh"), refresh.RequestUri);
    session.Dispose();
  }

  [Fact]
  public async Task RestoreAuthSnapshot_WhenSessionIsDisposed_ThrowsObjectDisposed()
  {
    var handler = new RecordingHttpMessageHandler(TokenIssuingResponder());
    var session = CreateSession(handler, new FakeTimeProvider());

    var login = await session.SignIn(
      new InteractiveSignInRequest { Email = "u@test.test", Password = "pw" },
      TestContext.Current.CancellationToken);
    Assert.Equal(InteractiveLoginStatus.Authenticated, login.Status);
    session.Dispose();

    // A snapshot carrying a distinct token pair, so a restore that slipped past the guard shows up
    // against the pair this session already holds.
    var exception = await Assert.ThrowsAsync<ObjectDisposedException>(
      () => session.RestoreAuthSnapshot(ExpiredSnapshot(bearer: "access-stale", refresh: "refresh-stale")));

    // Without the throw, the restore wrote the snapshot's pair into the auth state while the terminal
    // state suppressed the state change, so the session reported a token it could never use and
    // raised nothing. A host that restored onto an evicted target would boot unauthenticated with
    // nothing to point at.
    Assert.Contains(nameof(ControlrAuthSession), exception.ObjectName, StringComparison.Ordinal);
    Assert.Equal("access-1", session.GetAuthSnapshot().BearerToken);
    Assert.Equal(ControlrAuthSessionState.Disposed, session.State);
  }

  [Fact]
  public async Task RunRefreshLoop_WhenRefreshFailsRepeatedly_ExpiresSessionAfterRetryCap()
  {
    var timeProvider = new FakeTimeProvider();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var session = CreateSession(
      new RecordingHttpMessageHandler(_ => throw new HttpRequestException("Simulated persistent failure.")),
      timeProvider,
      options => options.BearerRefreshLeadTime = TimeSpan.FromMilliseconds(1));

    await session.RestoreAuthSnapshot(ExpiredSnapshot("access-old", "refresh-old"));

    var waitResult = await Waiter.Default.WaitFor(
      () => session.State == ControlrAuthSessionState.Expired,
      cancellationToken: cts.Token);
    Assert.True(waitResult);

    Assert.Equal(ControlrAuthSessionState.Expired, session.State);
    // After a dead-broken refresh, the session ends expired with tokens cleared rather than
    // retrying forever.
    Assert.Null(session.GetAuthSnapshot().RefreshToken);
    session.Dispose();
  }

  [Fact]
  public async Task RunRefreshLoop_WhenRefreshFailsTransiently_RetriesAndKeepsSession()
  {
    var failuresRemaining = 1;
    HttpResponseMessage Responder(HttpRequestMessage request)
    {
      if (request.RequestUri!.AbsolutePath.EndsWith("/refresh", StringComparison.Ordinal))
      {
        if (Interlocked.Decrement(ref failuresRemaining) >= 0)
        {
          throw new HttpRequestException("Simulated transient network failure.");
        }

        return RecordingHttpMessageHandler.Json(JsonSerializer.Serialize(Tokens("access-renewed", "refresh-renewed")));
      }

      return new HttpResponseMessage(HttpStatusCode.OK);
    }

    var timeProvider = new FakeTimeProvider();
    var session = CreateSession(
      new RecordingHttpMessageHandler(Responder),
      timeProvider,
      options => options.BearerRefreshLeadTime = TimeSpan.FromMilliseconds(1));

    await session.RestoreAuthSnapshot(ExpiredSnapshot("access-old", "refresh-old"));

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var waitResult = await Waiter.Default.WaitFor(
      () => Equal(session.GetAuthSnapshot().BearerToken, "access-renewed"),
      cancellationToken: cts.Token);
    Assert.True(waitResult);

    // The regression under test: a transient error must never expire the session or wipe the
    // still-server-valid refresh token.
    Assert.Equal(ControlrAuthSessionState.Authenticated, session.State);
    Assert.Equal("refresh-renewed", session.GetAuthSnapshot().RefreshToken);
    session.Dispose();
  }

  [Fact]
  public async Task RunRefreshLoop_WhenRefreshUnauthorized_ExpiresSession()
  {
    var timeProvider = new FakeTimeProvider();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var session = CreateSession(
      new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)),
      timeProvider,
      options => options.BearerRefreshLeadTime = TimeSpan.FromMilliseconds(1));

    await session.RestoreAuthSnapshot(ExpiredSnapshot("access-old", "refresh-old"));

    var waitResult = await Waiter.Default.WaitFor(
      () => session.State == ControlrAuthSessionState.Expired,
      cancellationToken: cts.Token);
    Assert.True(waitResult);

    Assert.Equal(ControlrAuthSessionState.Expired, session.State);
    Assert.Null(session.GetAuthSnapshot().RefreshToken);
    session.Dispose();
  }

  [Fact]
  public async Task RunRefreshLoop_WhenTokensAreClearedOutsideTheLoop_ExpiresSessionAndRaisesTheChange()
  {
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var handler = new GatedTokenRefreshHandler(gate.Task);
    var timeProvider = new FakeTimeProvider();
    var options = new ControlrApiClientOptions
    {
      BaseUrl = _server,
      BearerRefreshLeadTime = TimeSpan.FromMilliseconds(1)
    };

    var httpClient = new HttpClient(handler) { BaseAddress = _server };
    var authState = new ControlrApiClientAuthState(personalAccessToken: null);
    var refresher = new BearerTokenRefresher(
      authState,
      new SingleClientHttpClientFactory(httpClient),
      timeProvider);

    using var session = new ControlrAuthSession(
      new SingleClientHttpClientFactory(httpClient),
      authState,
      refresher,
      NullLogger<ControlrAuthSession>.Instance,
      new FrozenOptionsMonitor<ControlrApiClientOptions>(options),
      timeProvider);

    var raised = new List<ControlrAuthSessionState>();
    session.StateChanged += (_, args) => raised.Add(args.State);

    await session.RestoreAuthSnapshot(ExpiredSnapshot("access-old", "refresh-old"));

    // The lead time is already past, so the loop fires at once and blocks in the handler while
    // holding the refresh semaphore.
    Assert.True(await Waiter.Default.WaitFor(() => handler.RefreshStarted, cancellationToken: cts.Token));
    Assert.Equal(ControlrAuthSessionState.Authenticated, session.State);

    // This is what ControlrApi does when the server rejects a refresh during an ordinary call. It
    // clears the auth state it shares with the session and has no way to reach the state machine.
    authState.ClearBearerTokens();
    gate.SetResult();

    Assert.True(await Waiter.Default.WaitFor(
      () => session.State == ControlrAuthSessionState.Expired,
      cancellationToken: cts.Token));

    // The version bump from the clear makes the gated refresh's own result stale, so the loop
    // iterates, finds no expiry, and must expire the session rather than return silently. Returning
    // silently left State at Authenticated with no tokens and no loop, and pinned the factory target
    // because a live-looking session is exempt from idle eviction.
    Assert.False(session.IsAuthenticated);
    Assert.Null(session.GetAuthSnapshot().BearerToken);
    Assert.Contains(ControlrAuthSessionState.Expired, raised);
  }

  [Fact]
  public async Task SignIn_WhenServerRequiresTwoFactor_RequiresCodeThenAuthenticates()
  {
    var loginCount = 0;
    HttpResponseMessage Responder(HttpRequestMessage request)
    {
      if (request.RequestUri!.AbsolutePath.EndsWith("/interactive-login", StringComparison.Ordinal))
      {
        var first = Interlocked.Increment(ref loginCount) == 1;
        var payload = first
          ? new InteractiveLoginResponseDto(RequiresTwoFactor: true)
          : new InteractiveLoginResponseDto(
            false,
            Tokens: Tokens("access-1", "refresh-1"));
        return RecordingHttpMessageHandler.Json(JsonSerializer.Serialize(payload));
      }

      return new HttpResponseMessage(HttpStatusCode.OK);
    }

    var session = CreateSession(new RecordingHttpMessageHandler(Responder), new FakeTimeProvider());

    var first = await session.SignIn(
      new InteractiveSignInRequest { Email = "u@test.test", Password = "pw" },
      TestContext.Current.CancellationToken);
    Assert.Equal(InteractiveLoginStatus.RequiresTwoFactor, first.Status);
    Assert.True(session.RequiresTwoFactor);

    var second = await session.SignIn(
      new InteractiveSignInRequest { Email = "u@test.test", Password = "pw", TwoFactorCode = "123456" },
      TestContext.Current.CancellationToken);
    Assert.Equal(InteractiveLoginStatus.Authenticated, second.Status);
    Assert.Equal(ControlrAuthSessionState.Authenticated, session.State);
    session.Dispose();
  }

  private static ControlrAuthSession CreateSession(
    HttpMessageHandler handler,
    FakeTimeProvider timeProvider,
    Action<ControlrApiClientOptions>? configure = null)
  {
    var options = new ControlrApiClientOptions { BaseUrl = _server };
    configure?.Invoke(options);
    var client = new HttpClient(handler) { BaseAddress = _server };
    var authState = new ControlrApiClientAuthState(personalAccessToken: null);
    var refresher = new BearerTokenRefresher(authState, new SingleClientHttpClientFactory(client), timeProvider);
    return new ControlrAuthSession(
      new SingleClientHttpClientFactory(client),
      authState,
      refresher,
      NullLogger<ControlrAuthSession>.Instance,
      new FrozenOptionsMonitor<ControlrApiClientOptions>(options),
      timeProvider);
  }

  private static bool Equal(string? actual, string? expected) =>
    StringComparer.Ordinal.Equals(actual, expected);

  private static AuthSnapshot ExpiredSnapshot(string bearer, string refresh) =>
    new(null, bearer, DateTimeOffset.UnixEpoch, refresh);

  private static Func<HttpRequestMessage, HttpResponseMessage> RefreshOnlyResponder()
  {
    return request =>
      request.RequestUri!.AbsolutePath.EndsWith("/refresh", StringComparison.Ordinal)
        ? RecordingHttpMessageHandler.Json(
          JsonSerializer.Serialize(Tokens("access-renewed", "refresh-renewed")))
        : new HttpResponseMessage(HttpStatusCode.OK);
  }

  private static Func<HttpRequestMessage, HttpResponseMessage> TokenIssuingResponder()
  {
    return request =>
      request.RequestUri!.AbsolutePath.EndsWith("/interactive-login", StringComparison.Ordinal)
        ? RecordingHttpMessageHandler.Json(JsonSerializer.Serialize(
          new InteractiveLoginResponseDto(false, Tokens: Tokens("access-1", "refresh-1"))))
        : new HttpResponseMessage(HttpStatusCode.OK);
  }

  private static AccessTokenResponseDto Tokens(string accessToken, string refreshToken) =>
    new("Bearer", accessToken, 3600, refreshToken);

  /// <summary>
  /// Blocks the interactive login until released, then answers it with a valid token response. Used to
  /// hold a sign-in on the wire while the session is disposed underneath it.
  /// </summary>
  private sealed class GatedLoginHandler(Task gate) : HttpMessageHandler
  {
    public bool LoginStarted { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      if (!request.RequestUri!.AbsolutePath.EndsWith("/interactive-login", StringComparison.Ordinal))
      {
        return new HttpResponseMessage(HttpStatusCode.OK);
      }

      LoginStarted = true;
      await gate.WaitAsync(cancellationToken);
      return RecordingHttpMessageHandler.Json(JsonSerializer.Serialize(
        new InteractiveLoginResponseDto(false, Tokens: Tokens("access-late", "refresh-late"))));
    }
  }
  private sealed class GatedRefreshHandler(Task gate) : HttpMessageHandler
  {
    public bool RefreshStarted { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      if (request.RequestUri!.AbsolutePath.EndsWith("/refresh", StringComparison.Ordinal))
      {
        RefreshStarted = true;
        await gate.WaitAsync(cancellationToken);
      }

      return new HttpResponseMessage(HttpStatusCode.OK);
    }
  }

  /// <summary>
  /// Blocks the refresh until released, then answers it with a valid token response. Used to hold the
  /// refresh semaphore while another component changes the auth state underneath the loop.
  /// </summary>
  private sealed class GatedTokenRefreshHandler(Task gate) : HttpMessageHandler
  {
    public bool RefreshStarted { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      if (!request.RequestUri!.AbsolutePath.EndsWith("/refresh", StringComparison.Ordinal))
      {
        return new HttpResponseMessage(HttpStatusCode.OK);
      }

      RefreshStarted = true;
      await gate.WaitAsync(cancellationToken);
      return RecordingHttpMessageHandler.Json(
        JsonSerializer.Serialize(Tokens("access-gated", "refresh-gated")));
    }
  }
}
