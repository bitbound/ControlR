using System.Net;
using System.Text.Json;
using ControlR.ApiClient.Auth;
using ControlR.ApiClient.Tests.Helpers;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ControlR.ApiClient.Tests;

public sealed class ControlrApiClientFactoryTests
{
  private static readonly Uri _serverA = new("https://server-a.test/");
  private static readonly Uri _serverB = new("https://server-b.test/");

  [Fact]
  public void Dispose_DisposesTrackedEntries()
  {
    RecordingHttpMessageHandler? handler = null;
    var factory = CreateFactory(options =>
    {
      options.HttpMessageHandlerFactory = () => handler = new RecordingHttpMessageHandler();
    });
    factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);

    factory.Dispose();

    Assert.Equal(1, handler!.DisposeCount);
  }

  [Fact]
  public void Dispose_WhenCalledTwice_IsNoOp()
  {
    var factory = CreateFactory();
    factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);

    factory.Dispose();
    factory.Dispose();
  }

  [Fact]
  public async Task ExecuteApiCall_WhenUnauthorized_RenewsTokenAndRetriesAgainstSameServer()
  {
    var handlers = new List<RecordingHttpMessageHandler>();
    var deviceCallCount = 0;

    HttpResponseMessage Responder(HttpRequestMessage request)
    {
      if (request.RequestUri!.AbsolutePath.EndsWith("/refresh", StringComparison.Ordinal))
      {
        var payload = new AccessTokenResponseDto("Bearer", "renewed-token", 3600, "renewed-refresh");
        return RecordingHttpMessageHandler.Json(JsonSerializer.Serialize(payload));
      }

      deviceCallCount++;
      return deviceCallCount == 1
        ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
        : new HttpResponseMessage(HttpStatusCode.OK);
    }

    using var factory = CreateFactory(options =>
    {
      options.HttpMessageHandlerFactory = () =>
      {
        var handler = new RecordingHttpMessageHandler(Responder);
        handlers.Add(handler);
        return handler;
      };
    });

    var client = factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);
    var session = factory.GetOrCreateAuthSession("a");
    await session.RestoreAuthSnapshot(new AuthSnapshot(
      null,
      "initial-token",
      DateTimeOffset.UtcNow.AddHours(1),
      "initial-refresh"));

    var result = await client.Internal.Devices.DeleteDevice(
      Guid.NewGuid(),
      TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess);
    Assert.Equal(2, deviceCallCount);

    // handlers[0] is the authenticated client, handlers[1] the per-target unauthenticated client.
    var refreshRequest = Assert.Single(handlers[1].Requests);
    Assert.Equal(new Uri(_serverA, "/api/auth/refresh"), refreshRequest.RequestUri);

    var retriedRequest = handlers[0].Requests.ToArray()[^1];
    Assert.Equal("Bearer", retriedRequest.Headers.Authorization?.Scheme);
    Assert.Equal("renewed-token", retriedRequest.Headers.Authorization?.Parameter);
  }

  [Fact]
  public void GetClientNames_ReturnsCurrentTargets()
  {
    using var factory = CreateFactory();
    factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);
    factory.GetOrCreateClient("b", o => o.BaseUrl = _serverB);

    Assert.Equal(["a", "b"], factory.GetClientNames().OrderBy(x => x));

    factory.TryRemoveClient("a");
    Assert.Equal(["b"], factory.GetClientNames());
  }

  [Fact]
  public void GetOrCreateAuthSession_ReturnsSameSessionPerName()
  {
    using var factory = CreateFactory();
    factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);

    var first = factory.GetOrCreateAuthSession("a");
    var second = factory.GetOrCreateAuthSession("a");

    Assert.Same(first, second);
  }

  [Fact]
  public void GetOrCreateAuthSession_TwoTargets_TokensDoNotCross()
  {
    using var factory = CreateFactory();
    factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);
    factory.GetOrCreateClient("b", o => o.BaseUrl = _serverB);

    factory.GetOrCreateAuthSession("a").SetPersonalAccessToken("pat-a");
    factory.GetOrCreateAuthSession("b").SetPersonalAccessToken("pat-b");

    Assert.Equal("pat-a", factory.GetOrCreateAuthSession("a").PersonalAccessToken);
    Assert.Equal("pat-b", factory.GetOrCreateAuthSession("b").PersonalAccessToken);
  }

  [Fact]
  public void GetOrCreateAuthSession_WhenCalledConcurrently_BuildsSingleSession()
  {
    using var factory = CreateFactory();
    factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);

    var sessions = new IControlrAuthSession[8];
    Parallel.For(0, 8, i => sessions[i] = factory.GetOrCreateAuthSession("a"));

    Assert.All(sessions, session => Assert.Same(sessions[0], session));
  }

  [Fact]
  public void GetOrCreateAuthSession_WhenNameUnknown_Throws()
  {
    using var factory = CreateFactory();

    Assert.Throws<InvalidOperationException>(() => factory.GetOrCreateAuthSession("missing"));
  }

  [Fact]
  public void GetOrCreateClient_ReturnsDistinctClients_PerName()
  {
    using var factory = CreateFactory();

    var first = factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);
    var second = factory.GetOrCreateClient("b", o => o.BaseUrl = _serverB);

    Assert.NotSame(first, second);
  }

  [Fact]
  public void GetOrCreateClient_ReturnsSameCachedInstance_ForSameName()
  {
    using var factory = CreateFactory();

    var first = factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);
    var second = factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);

    Assert.Same(first, second);
  }

  [Fact]
  public void GetOrCreateClient_WhenBaseUrlMissing_ThrowsOptionsValidation()
  {
    using var factory = CreateFactory();

    var action = () => factory.GetOrCreateClient("a", o => o.PersonalAccessToken = "pat");

    Assert.Throws<OptionsValidationException>(action);
    Assert.Empty(factory.GetClientNames());
  }

  [Fact]
  public void GetOrCreateClient_WhenBaseUrlRelative_ThrowsOptionsValidation()
  {
    using var factory = CreateFactory();

    Assert.Throws<OptionsValidationException>(
      () => factory.GetOrCreateClient("a", o => o.BaseUrl = new Uri("/relative/", UriKind.Relative)));
    Assert.Empty(factory.GetClientNames());
  }

  [Fact]
  public void GetOrCreateClient_WhenCalledAfterDispose_ThrowsObjectDisposed()
  {
    var factory = CreateFactory();
    factory.Dispose();

    Assert.Throws<ObjectDisposedException>(
      () => factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA));
    Assert.Throws<ObjectDisposedException>(() => factory.TryRemoveClient("a"));
    Assert.Throws<ObjectDisposedException>(() => factory.GetOrCreateAuthSession("a"));
    Assert.Throws<ObjectDisposedException>(() => factory.GetClientNames());
  }

  [Fact]
  public void GetOrCreateClient_WhenCalledConcurrently_BuildsSingleEntry()
  {
    var handlerCreations = 0;
    using var factory = CreateFactory(options =>
    {
      options.HttpMessageHandlerFactory = () =>
      {
        Interlocked.Increment(ref handlerCreations);
        return new RecordingHttpMessageHandler();
      };
    });

    var results = new IControlrApi[8];
    Parallel.For(0, 8, i =>
    {
      results[i] = factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);
    });

    // One entry built => exactly two handlers (authenticated + unauthenticated).
    Assert.Equal(2, handlerCreations);
    Assert.All(results, client => Assert.Same(results[0], client));
  }

  [Fact]
  public void GetOrCreateClient_WhenCapReached_EvictsLeastRecentlyUsedNotJustCreated()
  {
    var timeProvider = new FakeTimeProvider();
    using var factory = CreateFactory(
      options =>
      {
        options.MaxTrackedClients = 2;
        options.MaxIdleClientLifetime = null;
      },
      timeProvider);

    factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);
    factory.GetOrCreateClient("b", o => o.BaseUrl = _serverB);

    // Touch "b" so it becomes the most recently used of the two existing entries.
    factory.GetOrCreateClient("b", o => o.BaseUrl = _serverB);
    timeProvider.Advance(TimeSpan.FromMinutes(1));

    factory.GetOrCreateClient("c", o => o.BaseUrl = new Uri("https://server-c.test/"));

    Assert.Equal(["b", "c"], factory.GetClientNames().OrderBy(x => x));
  }

  [Fact]
  public void GetOrCreateClient_WhenHandlerFactoryThrows_DisposesAllocatedHandlers()
  {
    RecordingHttpMessageHandler? firstHandler = null;
    var calls = 0;
    using var factory = CreateFactory(options =>
    {
      options.HttpMessageHandlerFactory = () =>
      {
        calls++;
        if (calls == 1)
        {
          firstHandler = new RecordingHttpMessageHandler();
          return firstHandler;
        }

        throw new InvalidOperationException("Simulated handler construction failure.");
      };
    });

    Assert.Throws<InvalidOperationException>(
      () => factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA));

    // The handler allocated before the failure must not leak.
    Assert.Equal(1, firstHandler!.DisposeCount);
    Assert.Empty(factory.GetClientNames());
  }

  [Fact]
  public async Task GetOrCreateClient_WhenNameExists_IgnoresNewConfigureAction()
  {
    var handlerCount = 0;
    RecordingHttpMessageHandler? firstHandler = null;
    using var factory = CreateFactory(options =>
    {
      options.HttpMessageHandlerFactory = () =>
      {
        handlerCount++;
        var handler = new RecordingHttpMessageHandler();
        firstHandler ??= handler;
        return handler;
      };
    });

    factory.GetOrCreateClient("a", o =>
    {
      o.BaseUrl = _serverA;
      o.PersonalAccessToken = "pat-first";
    });
    factory.GetOrCreateClient("a", o =>
    {
      o.BaseUrl = new Uri("https://other.test/");
      o.PersonalAccessToken = "pat-second";
    });

    // Two handlers per entry: authenticated + unauthenticated. Only one entry exists.
    Assert.Equal(2, handlerCount);

    var client = factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);
    await client.Internal.Devices.DeleteDevice(
      Guid.NewGuid(),
      TestContext.Current.CancellationToken);

    var request = Assert.Single(firstHandler!.Requests);
    Assert.Equal(
      _serverA.GetLeftPart(UriPartial.Authority),
      request.RequestUri!.GetLeftPart(UriPartial.Authority));
    Assert.Equal(
      "pat-first",
      request.Headers.GetValues(ControlrApiClientOptions.PersonalAccessTokenHeader).Single());
  }

  [Fact]
  public async Task RefreshIfNeeded_WhenSemaphoreDisposedMidFlight_StillCompletesRefresh()
  {
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var handler = new GatedRefreshHandlerInline(gate.Task);
    var client = new HttpClient(handler) { BaseAddress = new Uri("https://server.test/") };
    var authState = new ControlrApiClientAuthState(personalAccessToken: null);
    authState.SetBearerTokens("access-old", "refresh-old", DateTimeOffset.UnixEpoch);

    var refresher = new BearerTokenRefresher(
      authState,
      new SingleClientHttpClientFactory(client),
      TimeProvider.System);

    var refreshTask = refresher.RefreshIfNeeded(
      forceRefresh: true,
      refreshWindow: TimeSpan.FromMinutes(1),
      cancellationToken: TestContext.Current.CancellationToken);

    await WaitUntilAsyncInline(() => handler.RefreshStarted);

    // Entry teardown disposes the lock while the refresh holds it.
    authState.BearerRefreshLock.Dispose();
    gate.SetResult();

    // Pre-fix, the finally-block Release threw ObjectDisposedException out of RefreshIfNeeded.
    var result = await refreshTask;

    Assert.Equal(BearerTokenRefreshResult.Refreshed, result);
    client.Dispose();
  }

  [Fact]
  public void SweepIdleClients_WhenEntryIdleBeyondWindow_EvictsEntry()
  {
    var timeProvider = new FakeTimeProvider();
    var handlers = new List<RecordingHttpMessageHandler>();
    using var factory = CreateFactory(
      options =>
      {
        options.MaxIdleClientLifetime = TimeSpan.FromMinutes(5);
        options.HttpMessageHandlerFactory = () =>
        {
          var handler = new RecordingHttpMessageHandler();
          handlers.Add(handler);
          return handler;
        };
      },
      timeProvider);

    factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);
    timeProvider.Advance(TimeSpan.FromMinutes(6));

    ((ControlrApiClientFactory)factory).SweepIdleClients();

    Assert.Empty(factory.GetClientNames());
    Assert.All(handlers, handler => Assert.Equal(1, handler.DisposeCount));
  }

  [Fact]
  public void SweepIdleClients_WhenEntryTouchedBetweenSweeps_KeepsEntry()
  {
    var timeProvider = new FakeTimeProvider();
    using var factory = CreateFactory(
      options => options.MaxIdleClientLifetime = TimeSpan.FromMinutes(5),
      timeProvider);

    factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);
    timeProvider.Advance(TimeSpan.FromMinutes(3));
    factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);
    timeProvider.Advance(TimeSpan.FromMinutes(3));

    ((ControlrApiClientFactory)factory).SweepIdleClients();

    Assert.Equal(["a"], factory.GetClientNames());
  }

  [Fact]
  public void SweepIdleClients_WhenLifetimeNull_KeepsAllEntries()
  {
    var timeProvider = new FakeTimeProvider();
    using var factory = CreateFactory(
      options => options.MaxIdleClientLifetime = null,
      timeProvider);

    factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);
    timeProvider.Advance(TimeSpan.FromDays(30));

    ((ControlrApiClientFactory)factory).SweepIdleClients();

    Assert.Equal(["a"], factory.GetClientNames());
  }

  [Fact]
  public void TryRemoveClient_WhenNameMissing_ReturnsFalse()
  {
    using var factory = CreateFactory();

    Assert.False(factory.TryRemoveClient("nope"));
  }

  [Fact]
  public async Task TryRemoveClient_WhenNamePresent_AllowsRecreateWithNewOptions()
  {
    var handlers = new List<RecordingHttpMessageHandler>();
    using var factory = CreateFactory(options =>
    {
      options.HttpMessageHandlerFactory = () =>
      {
        var handler = new RecordingHttpMessageHandler();
        handlers.Add(handler);
        return handler;
      };
    });

    factory.GetOrCreateClient("a", o =>
    {
      o.BaseUrl = _serverA;
      o.PersonalAccessToken = "pat-old";
    });

    Assert.True(factory.TryRemoveClient("a"));
    // Index 0 is the authenticated handler, index 1 the unauthenticated one for the first entry.
    Assert.Equal(1, handlers[0].DisposeCount);

    var rotated = factory.GetOrCreateClient("a", o =>
    {
      o.BaseUrl = _serverA;
      o.PersonalAccessToken = "pat-new";
    });
    var rotatedResult = await rotated.Internal.Devices.DeleteDevice(
      Guid.NewGuid(),
      TestContext.Current.CancellationToken);
    Assert.True(rotatedResult.IsSuccess);

    Assert.Empty(handlers[0].Requests);
    var request = Assert.Single(handlers[2].Requests);
    Assert.Equal(
      "pat-new",
      request.Headers.GetValues(ControlrApiClientOptions.PersonalAccessTokenHeader).Single());
  }

  private static ControlrApiClientFactory CreateFactory(
    Action<ControlrApiClientFactoryOptions>? configure = null,
    TimeProvider? timeProvider = null)
  {
    var options = new ControlrApiClientFactoryOptions();
    configure?.Invoke(options);
    return new ControlrApiClientFactory(
      options,
      timeProvider ?? TimeProvider.System,
      NullLoggerFactory.Instance);
  }

  private static async Task WaitUntilAsyncInline(Func<bool> condition)
  {
    var deadline = DateTime.UtcNow.AddSeconds(10);
    while (!condition())
    {
      if (DateTime.UtcNow > deadline)
      {
        throw new TimeoutException("The condition was not met within the allotted time.");
      }

      await Task.Delay(10);
    }
  }

  private sealed class GatedRefreshHandlerInline(Task gate) : HttpMessageHandler
  {
    public bool RefreshStarted { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      RefreshStarted = true;
      await gate.WaitAsync(cancellationToken);
      return RecordingHttpMessageHandler.Json(
        System.Text.Json.JsonSerializer.Serialize(
          new ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal.AccessTokenResponseDto(
            "Bearer",
            "access-renewed",
            3600,
            "refresh-renewed")));
    }
  }
}
