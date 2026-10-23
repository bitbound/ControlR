using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlR.ApiClient.Auth;
using ControlR.ApiClient.Internal;
using ControlR.ApiClient.Tests.Helpers;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Waiter = ControlR.Libraries.Shared.Services.Waiter;

namespace ControlR.ApiClient.Tests;

/// <summary>
/// Guards against the two /api/auth/refresh storm shapes reported from a third-party product that
/// embeds the Avalonia viewer with interactive bearer auth: a background loop hammering the endpoint
/// at request rate when the server's token lifetime is at or below the refresh lead time, and a
/// concurrent-401 burst paying one refresh POST per waiter. Every handler here is request-capped with
/// a terminal answer past the cap, so a regressed loop starves itself instead of spinning against the
/// machine.
/// </summary>
public sealed class RefreshTokenStormTests
{
  private static readonly Uri _server = new("https://server.test/");

  [Fact]
  public async Task ExecuteApiCall_WhenConcurrentCallsAllGet401_PostsOneSharedRefresh()
  {
    // The storm shape: N concurrent calls that each take a 401 queue on the refresh semaphore.
    // Each waiter recorded the token version at its own send, so every waiter after the first rides
    // the first waiter's renewal instead of stacking its own POST on the stateless endpoint.
    const int concurrency = 10;

    var handler = new StormHandler(concurrency);
    using var factory = new ControlrApiClientFactory(
      new ControlrApiClientFactoryOptions
      {
        MaxIdleClientLifetime = null,
        HttpMessageHandlerFactory = () => handler
      },
      TimeProvider.System,
      NullLoggerFactory.Instance);

    var client = factory.GetOrCreateClient("a", o => o.BaseUrl = _server);
    var session = factory.GetOrCreateAuthSession("a");
    await session.RestoreAuthSnapshot(new AuthSnapshot(
      null,
      "initial-token",
      DateTimeOffset.UtcNow.AddHours(1),
      "initial-refresh"));

    var calls = Enumerable.Range(0, concurrency)
      .Select(_ => client.Internal.Devices.DeleteDevice(Guid.NewGuid(), TestContext.Current.CancellationToken))
      .ToList();

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    Assert.True(await Waiter.Default.WaitFor(
      () => handler.StaleCallCount >= concurrency,
      cancellationToken: cts.Token));

    foreach (var call in calls)
    {
      Assert.True((await call).IsSuccess);
    }

    // The queued waiters find the version moved under the lock and take the renewal that already
    // landed. A regressed loop pays one POST per waiter, which this assertion catches.
    Assert.Equal(1, handler.RefreshCount);
    session.Dispose();
  }

  [Fact]
  public async Task ExecuteApiCall_WhenRenewalLandsBeforeTheHeaderIsApplied_PostsAForcedRefresh()
  {
    // Deduping against the version sampled before the send understates it when a renewal lands in
    // that gap: the request goes out with the renewed token, and riding the renewal re-sends the
    // token the server just rejected. The dedupe has to measure against the generation the auth
    // header actually applied.
    var authState = new ControlrApiClientAuthState();
    authState.SetBearerTokens("token-1", "refresh-1", DateTimeOffset.UtcNow.AddHours(1), TimeSpan.FromHours(1));

    var responder = new RenewalRejectingResponder("token-2");
    var authHandler = new ControlrApiAuthHeaderHandler(authState) { InnerHandler = responder };
    using var client = new HttpClient(new SingleRenewalHandler(authState, "token-2") { InnerHandler = authHandler })
    {
      BaseAddress = _server
    };

    var refresher = new BearerTokenRefresher(authState, new SingleClientHttpClientFactory(client), TimeProvider.System);
    var api = (IControlrApi)new ControlrApi(
      client,
      authState,
      refresher,
      NullLogger<ControlrApi>.Instance,
      Options.Create(new ControlrApiClientOptions { BaseUrl = _server }));

    var result = await api.Internal.Devices.DeleteDevice(Guid.NewGuid(), TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess);
    Assert.Equal(1, responder.RefreshCount);
  }

  [Fact]
  public async Task ExecuteApiCall_WhenServerTokenLifetimeIsShorterThanLeadTime_DoesNotRefreshOnEveryCall()
  {
    // The proactive check runs before every API call. Against a server issuing 60-second tokens,
    // the raw 60-second lead time made every freshly renewed token look stale, so each call paid
    // for its own refresh POST. The window is capped at half the observed lifetime, leaving the
    // same token far outside the window.
    var handler = new PacedRefreshHandler(TimeSpan.FromSeconds(60), maxRefreshes: 50);
    var client = new HttpClient(handler) { BaseAddress = _server };
    var authState = new ControlrApiClientAuthState();
    var refresher = new BearerTokenRefresher(
      authState,
      new SingleClientHttpClientFactory(client),
      TimeProvider.System);
    var api = (IControlrApi)new ControlrApi(
      client,
      authState,
      refresher,
      NullLogger<ControlrApi>.Instance,
      Options.Create(new ControlrApiClientOptions { BaseUrl = _server }));

    authState.SetBearerTokenResponse(
      new AccessTokenResponseDto("Bearer", "access-1", 60, "refresh-1"),
      TimeProvider.System);

    for (var i = 0; i < 20; i++)
    {
      Assert.True(
        (await api.Internal.Devices.DeleteDevice(Guid.NewGuid(), TestContext.Current.CancellationToken))
        .IsSuccess);
    }

    Assert.Equal(0, handler.RefreshCount);
  }

  [Fact]
  public async Task ExecuteApiCall_WhenUnstamped401FollowsARenewal_PostsAForcedRefresh()
  {
    // The GetFromJsonAsync-shaped calls throw the BCL's own 401 exception, which carries no stamp.
    // A dedupe that cannot trust the version it measures must not ride a renewal on such a
    // challenge, so the forced refresh always pays the POST.
    var authState = new ControlrApiClientAuthState();
    authState.SetBearerTokens("token-1", "refresh-1", DateTimeOffset.UtcNow.AddHours(1), TimeSpan.FromHours(1));

    var responder = new RenewalRejectingResponder("token-2");
    var authHandler = new ControlrApiAuthHeaderHandler(authState) { InnerHandler = responder };
    using var client = new HttpClient(new SingleRenewalHandler(authState, "token-2") { InnerHandler = authHandler })
    {
      BaseAddress = _server
    };

    var refresher = new BearerTokenRefresher(authState, new SingleClientHttpClientFactory(client), TimeProvider.System);
    var api = new ControlrApi(
      client,
      authState,
      refresher,
      NullLogger<ControlrApi>.Instance,
      Options.Create(new ControlrApiClientOptions { BaseUrl = _server }));

    var result = await api.ExecuteApiCall(async () =>
    {
      var version = await client.GetFromJsonAsync<Version>("api/version/server", TestContext.Current.CancellationToken);
      Assert.NotNull(version);
    });

    Assert.True(result.IsSuccess);
    Assert.Equal(1, responder.RefreshCount);
  }

  [Fact]
  public async Task RunRefreshLoop_WhenServerTokenLifetimeIsShorterThanLeadTime_PacesRefreshesByTokenLifetime()
  {
    // The Aspire trace pattern: back-to-back successful refresh POSTs at request-round-trip
    // spacing. The loop's wait was (expiry - now - lead time) clamped to zero, so a 60-second
    // token against the client's 60-second lead time never slept, and nothing counts a successful
    // POST as a failure to back off from. The capped window turns that wait into half a lifetime.
    var handler = new PacedRefreshHandler(TimeSpan.FromSeconds(60), maxRefreshes: 50);
    var session = CreateSession(handler, TimeProvider.System);

    await session.RestoreAuthSnapshot(
      new AuthSnapshot(null, "access-old", DateTimeOffset.UnixEpoch, "refresh-old"));

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    Assert.True(await Waiter.Default.WaitFor(
      () => handler.RefreshCount >= 1,
      cancellationToken: cts.Token));

    await Task.Delay(TimeSpan.FromMilliseconds(250), cts.Token);

    // One renewal for the expired restore, and at most one more before the 30-second window.
    // A regressed loop blows past the handler's cap and expires itself on the terminal 401.
    Assert.InRange(handler.RefreshCount, 1, 2);
    Assert.Equal(ControlrAuthSessionState.Authenticated, session.State);
    session.Dispose();
  }

  private static ControlrAuthSession CreateSession(HttpMessageHandler handler, TimeProvider timeProvider)
  {
    var options = new ControlrApiClientOptions { BaseUrl = _server };
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

  /// <summary>
  /// Issues tokens with the given lifetime and counts refresh POSTs. Past the cap the refresh
  /// answers 401, which clears the tokens and ends any regressed loop instead of letting it spin
  /// against the machine. The one-millisecond yield keeps every loop iteration off its caller's
  /// thread, so a fire-and-forget loop cannot run inline into the caller.
  /// </summary>
  private sealed class PacedRefreshHandler(TimeSpan tokenLifetime, int maxRefreshes) : HttpMessageHandler
  {
    private int _refreshCount;

    public int RefreshCount => Volatile.Read(ref _refreshCount);

    protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken);

      if (request.RequestUri!.AbsolutePath.EndsWith("/refresh", StringComparison.Ordinal))
      {
        var n = Interlocked.Increment(ref _refreshCount);
        if (n > maxRefreshes)
        {
          return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        return RecordingHttpMessageHandler.Json(JsonSerializer.Serialize(
          new AccessTokenResponseDto("Bearer", $"access-{n}", (int)tokenLifetime.TotalSeconds, $"refresh-{n}")));
      }

      return new HttpResponseMessage(HttpStatusCode.OK);
    }
  }

  /// <summary>
  /// Answers 401 to the renewed token (a server-side rejection of the just-issued pair), issues a
  /// third token on the single allowed refresh POST, answers the version endpoint with a parseable
  /// payload, and authorizes everything else. Sets RequestMessage the way the real terminal handler
  /// does, so the 401 enrichment can read the generation the auth header handler stamped on the
  /// challenged request.
  /// </summary>
  private sealed class RenewalRejectingResponder(string rejectedToken) : HttpMessageHandler
  {
    private int _refreshCount;

    public int RefreshCount => Volatile.Read(ref _refreshCount);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      if (request.RequestUri!.AbsolutePath.EndsWith("/refresh", StringComparison.Ordinal))
      {
        Interlocked.Increment(ref _refreshCount);
        var refreshResponse = RecordingHttpMessageHandler.Json(JsonSerializer.Serialize(
          new AccessTokenResponseDto("Bearer", "token-3", 3600, "refresh-3")));
        refreshResponse.RequestMessage = request;
        return Task.FromResult(refreshResponse);
      }

      if (request.Headers.Authorization?.Parameter == rejectedToken)
      {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request });
      }

      if (request.RequestUri!.AbsolutePath.EndsWith("/version/server", StringComparison.Ordinal))
      {
        var versionResponse = RecordingHttpMessageHandler.Json("\"1.2.3\"");
        versionResponse.RequestMessage = request;
        return Task.FromResult(versionResponse);
      }

      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request });
    }
  }

  /// <summary>
  /// Lands one bearer renewal (a background-loop refresh completing) right before the auth header
  /// handler reads the state, then lets every request through untouched.
  /// </summary>
  private sealed class SingleRenewalHandler(ControlrApiClientAuthState authState, string renewedToken) : DelegatingHandler
  {
    private int _done;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      if (!request.RequestUri!.AbsolutePath.EndsWith("/refresh", StringComparison.Ordinal) &&
        Interlocked.Exchange(ref _done, 1) == 0)
      {
        authState.SetBearerTokens(
          renewedToken,
          "refresh-2",
          DateTimeOffset.UtcNow.AddHours(1),
          TimeSpan.FromHours(1));
      }

      return base.SendAsync(request, cancellationToken);
    }
  }

  /// <summary>
  /// Answers the first refresh only after every concurrent call has taken its 401, so all waiters
  /// are provably queued on the refresh semaphore against the same token version before the first
  /// renewal applies. Each refresh rotates the token pair, and non-refresh calls are authorized only
  /// by a renewed bearer. Past the cap the refresh answers 401 as the loop-starving kill switch.
  /// Sets RequestMessage the way the real terminal handler does, so the challenged calls carry the
  /// stamped generation the dedupe measures against.
  /// </summary>
  private sealed class StormHandler(int expectedStaleCalls) : HttpMessageHandler
  {
    private readonly TaskCompletionSource _releaseFirstRefresh = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _refreshCount;
    private int _staleCallCount;

    public int RefreshCount => Volatile.Read(ref _refreshCount);
    public int StaleCallCount => Volatile.Read(ref _staleCallCount);

    protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      if (request.RequestUri!.AbsolutePath.EndsWith("/refresh", StringComparison.Ordinal))
      {
        var n = Interlocked.Increment(ref _refreshCount);
        if (n > expectedStaleCalls + 5)
        {
          return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request };
        }

        if (n == 1)
        {
          await _releaseFirstRefresh.Task.WaitAsync(cancellationToken);
        }

        return RecordingHttpMessageHandler.Json(JsonSerializer.Serialize(
          new AccessTokenResponseDto("Bearer", $"renewed-{n}", 3600, $"renewed-refresh-{n}")));
      }

      var bearer = request.Headers.Authorization?.Parameter;
      if (bearer == "initial-token")
      {
        if (Interlocked.Increment(ref _staleCallCount) >= expectedStaleCalls)
        {
          _releaseFirstRefresh.TrySetResult();
        }

        return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request };
      }

      return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request };
    }
  }
}
