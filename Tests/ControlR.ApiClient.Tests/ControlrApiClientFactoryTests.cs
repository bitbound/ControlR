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
  public void Constructor_WhenMaxTrackedClientsIsBelowOne_ThrowsArgumentOutOfRangeException()
  {
    var zero = new ControlrApiClientFactoryOptions { MaxTrackedClients = 0 };
    var negative = new ControlrApiClientFactoryOptions { MaxTrackedClients = -5 };

    // A value below one used to mean "no limit", the opposite of what an operator setting a cap
    // intends, and nothing rejected it.
    Assert.Throws<ArgumentOutOfRangeException>(
      () => new ControlrApiClientFactory(zero, TimeProvider.System, NullLoggerFactory.Instance));
    Assert.Throws<ArgumentOutOfRangeException>(
      () => new ControlrApiClientFactory(negative, TimeProvider.System, NullLoggerFactory.Instance));
  }

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
  public async Task ExecuteApiCall_WhenTeardownBeganWhileAnotherRequestIsOutstanding_RefusesWithoutUsingTheStack()
  {
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var handlers = new List<GatedRequestHandlerInline>();
    using var factory = CreateFactory(options =>
    {
      options.MaxIdleClientLifetime = null;
      options.HttpMessageHandlerFactory = () =>
      {
        var handler = new GatedRequestHandlerInline(gate.Task);
        handlers.Add(handler);
        return handler;
      };
    });

    var client = factory.GetOrCreateClient("a", o =>
    {
      o.BaseUrl = _serverA;
      o.PersonalAccessToken = "pat";
    });

    var outstanding = client.Internal.Devices.DeleteDevice(
      Guid.NewGuid(),
      TestContext.Current.CancellationToken);

    await WaitUntilAsyncInline(() => handlers.Count > 0 && handlers[0].RequestStarted);

    // Removal publishes teardown, but the outstanding call keeps the stack alive. This is the window
    // the refused lease exists for: a call arriving now has to be refused, not issued into a stack
    // that the outstanding call's completion is about to release.
    Assert.True(factory.TryRemoveClient("a"));

    // The short timeout is a backstop: if the refused-lease guard regresses, this call reaches the
    // gated handler and would otherwise block the run instead of failing it.
    using var refusalTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    var refused = await client.Internal.Devices.DeleteDevice(
      Guid.NewGuid(),
      refusalTimeout.Token);

    Assert.False(refused.IsSuccess);
    Assert.Contains("was disposed", refused.Reason, StringComparison.Ordinal);

    // Without the refused lease, the call reaches the handler and this becomes two.
    Assert.Equal(1, handlers[0].RequestCount);

    gate.SetResult();

    Assert.True((await outstanding).IsSuccess);
    await WaitUntilAsyncInline(() => handlers[0].DisposeCount > 0);
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
  public async Task GetAllDevices_WhenTeardownBeganWhileAnotherRequestIsOutstanding_RefusesTheEnumeration()
  {
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var handlers = new List<GatedRequestHandlerInline>();
    using var factory = CreateFactory(options =>
    {
      options.MaxIdleClientLifetime = null;
      options.HttpMessageHandlerFactory = () =>
      {
        var handler = new GatedRequestHandlerInline(gate.Task);
        handlers.Add(handler);
        return handler;
      };
    });

    var client = factory.GetOrCreateClient("a", o =>
    {
      o.BaseUrl = _serverA;
      o.PersonalAccessToken = "pat";
    });

    var outstanding = client.Internal.Devices.DeleteDevice(
      Guid.NewGuid(),
      TestContext.Current.CancellationToken);

    await WaitUntilAsyncInline(() => handlers.Count > 0 && handlers[0].RequestStarted);

    Assert.True(factory.TryRemoveClient("a"));

    // The stack is still alive at this point, held by the outstanding call, so this is the case where
    // only the enumeration's own lease can refuse it. A streaming endpoint has no failed result to
    // return, and ending the sequence quietly would look exactly like a server that has no devices.
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    var ex = await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
    {
      await foreach (var device in client.Internal.Devices.GetAllDevices(timeout.Token))
      {
        Assert.Fail($"Expected no devices, but got {device}.");
      }
    });

    Assert.Contains("was disposed", ex.Message, StringComparison.Ordinal);

    gate.SetResult();

    Assert.True((await outstanding).IsSuccess);
    Assert.Equal(1, handlers[0].RequestCount);
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
  public async Task GetOrCreateClient_AfterTargetWasRemoved_DoesNotIssueTheRequestAtAll()
  {
    var handlers = new List<RecordingHttpMessageHandler>();
    using var factory = CreateFactory(options =>
    {
      options.MaxIdleClientLifetime = null;
      options.HttpMessageHandlerFactory = () =>
      {
        var handler = new RecordingHttpMessageHandler();
        handlers.Add(handler);
        return handler;
      };
    });

    var client = factory.GetOrCreateClient("a", o =>
    {
      o.BaseUrl = _serverA;
      o.PersonalAccessToken = "pat";
    });

    Assert.True(factory.TryRemoveClient("a"));

    var staleResult = await client.Internal.Devices.DeleteDevice(
      Guid.NewGuid(),
      TestContext.Current.CancellationToken);

    Assert.False(staleResult.IsSuccess);

    // Nothing was in flight, so removal released the stack synchronously and the caller is told the
    // HTTP stack is gone rather than being handed a server-fault-shaped failure. The refused-lease
    // guard itself is pinned by ExecuteApiCall_WhenTeardownBeganWhileAnotherRequestIsOutstanding,
    // where the stack is still alive and only the guard can stop the call.
    Assert.NotEmpty(handlers);
    Assert.All(handlers, handler => Assert.Empty(handler.Requests));
  }

  [Fact]
  public async Task GetOrCreateClient_AfterTargetWasRemoved_ReportsDisposedTarget()
  {
    using var factory = CreateFactory(options => options.MaxIdleClientLifetime = null);
    var client = factory.GetOrCreateClient("a", o =>
    {
      o.BaseUrl = _serverA;
      o.PersonalAccessToken = "pat";
    });

    Assert.True(factory.TryRemoveClient("a"));

    // A caller holding the removed reference gets a failure that names the disposed HTTP stack, not
    // one shaped like a server fault.
    var staleResult = await client.Internal.Devices.DeleteDevice(
      Guid.NewGuid(),
      TestContext.Current.CancellationToken);
    Assert.False(staleResult.IsSuccess);
    Assert.Contains("was disposed", staleResult.Reason, StringComparison.Ordinal);
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
  public async Task RefreshIfNeeded_WhenTargetWasAlreadyRemoved_ThrowsInsteadOfReportingNoRefreshNeeded()
  {
    var authState = new ControlrApiClientAuthState(personalAccessToken: null);
    authState.SetBearerTokens("access-old", "refresh-old", DateTimeOffset.UnixEpoch);
    using var client = new HttpClient(new RecordingHttpMessageHandler());

    var tracker = new InFlightTracker();
    tracker.RequestTeardown();

    var refresher = new BearerTokenRefresher(
      authState,
      new SingleClientHttpClientFactory(client),
      TimeProvider.System)
    {
      Requests = tracker
    };

    // NoRefreshNeeded reads as a healthy outcome wherever it is returned. The background refresh loop
    // resets its failure count on that value and would go on polling a target that no longer exists,
    // and a caller asking for a bearer token would be handed one it has been told is expired.
    var ex = await Assert.ThrowsAsync<ObjectDisposedException>(
      () => refresher.RefreshIfNeeded(
        forceRefresh: true,
        refreshWindow: TimeSpan.FromMinutes(1),
        cancellationToken: TestContext.Current.CancellationToken));

    Assert.Contains("was disposed", ex.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task RefreshIfNeeded_WhileTrackedTargetIsTornDown_DefersTheReleaseUntilTheRefreshCompletes()
  {
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var handler = new GatedRefreshHandlerInline(gate.Task);
    using var client = new HttpClient(handler) { BaseAddress = new Uri("https://server.test/") };
    var authState = new ControlrApiClientAuthState(personalAccessToken: null);
    authState.SetBearerTokens("access-old", "refresh-old", DateTimeOffset.UnixEpoch);

    var released = 0;
    var tracker = new InFlightTracker();
    tracker.AttachRelease(() => Interlocked.Increment(ref released));

    var refresher = new BearerTokenRefresher(
      authState,
      new SingleClientHttpClientFactory(client),
      TimeProvider.System)
    {
      Requests = tracker
    };

    var refreshTask = refresher.RefreshIfNeeded(
      forceRefresh: true,
      refreshWindow: TimeSpan.FromMinutes(1),
      cancellationToken: TestContext.Current.CancellationToken);

    await WaitUntilAsyncInline(() => handler.RefreshStarted);

    tracker.RequestTeardown();

    // Releasing the stack here disposes the refresh lock, and SemaphoreSlim never resumes a waiter
    // that was already queued when the dispose landed. A caller waiting on a bearer token would hang
    // with no exception. Session-driven refreshes are counted for this reason.
    Assert.Equal(0, Volatile.Read(ref released));

    gate.SetResult();

    Assert.Equal(BearerTokenRefreshResult.Refreshed, await refreshTask);
    Assert.Equal(1, Volatile.Read(ref released));
  }

  [Fact]
  public async Task RestoreAuthSnapshot_WhenTargetWasRemoved_ThrowsInsteadOfReportingARestoredSession()
  {
    using var factory = CreateFactory(options => options.MaxIdleClientLifetime = null);
    factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);
    var session = factory.GetOrCreateAuthSession("a");

    Assert.True(factory.TryRemoveClient("a"));

    // Restoring into a session whose target is gone would report IsAuthenticated with tokens that
    // nothing can ever renew, while every call that uses them fails.
    await Assert.ThrowsAsync<ObjectDisposedException>(
      () => session.RestoreAuthSnapshot(new AuthSnapshot("pat", null, null, null)));
  }

  [Fact]
  public async Task SignIn_WhileTargetIsBeingRemoved_CompletesTheLoginAndThenReleases()
  {
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var handlers = new List<GatedLoginHandlerInline>();
    using var factory = CreateFactory(options =>
    {
      options.MaxIdleClientLifetime = null;
      options.HttpMessageHandlerFactory = () =>
      {
        var handler = new GatedLoginHandlerInline(gate.Task);
        handlers.Add(handler);
        return handler;
      };
    });

    factory.GetOrCreateClient("a", o => o.BaseUrl = _serverA);
    var session = factory.GetOrCreateAuthSession("a");

    var signIn = session.SignIn(
      new InteractiveSignInRequest
      {
        Email = "user@test.test",
        Password = "password"
      },
      TestContext.Current.CancellationToken);

    await WaitUntilAsyncInline(() => handlers.Any(handler => handler.RequestStarted));
    var loginHandler = handlers.First(handler => handler.RequestStarted);

    Assert.True(factory.TryRemoveClient("a"));

    // Releasing the client here cancelled the sign-in, and the session's catch-all reported that as
    // bad credentials for a login the server had already accepted.
    Assert.Equal(0, loginHandler.DisposeCount);

    gate.SetResult();

    var result = await signIn;

    Assert.Equal(InteractiveLoginStatus.Authenticated, result.Status);
    await WaitUntilAsyncInline(() => loginHandler.DisposeCount > 0);
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

  [Fact]
  public async Task TryRemoveClient_WhileRequestIsInFlight_LetsTheRequestFinishThenReleases()
  {
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var handlers = new List<GatedRequestHandlerInline>();
    using var factory = CreateFactory(options =>
    {
      options.MaxIdleClientLifetime = null;
      options.HttpMessageHandlerFactory = () =>
      {
        var handler = new GatedRequestHandlerInline(gate.Task);
        handlers.Add(handler);
        return handler;
      };
    });

    var client = factory.GetOrCreateClient("a", o =>
    {
      o.BaseUrl = _serverA;
      o.PersonalAccessToken = "pat";
    });

    var call = client.Internal.Devices.DeleteDevice(Guid.NewGuid(), TestContext.Current.CancellationToken);
    await WaitUntilAsyncInline(() => handlers.Count > 0 && handlers[0].RequestStarted);

    Assert.True(factory.TryRemoveClient("a"));
    gate.SetResult();

    // Disposing the HTTP stack while the call was still out there cancelled it, so a healthy
    // server was reported to the caller as a failed request.
    var result = await call;
    Assert.True(result.IsSuccess, result.Reason);

    // The stack is released once the last in-flight request drains.
    await WaitUntilAsyncInline(() => handlers[0].DisposeCount > 0);
    Assert.Equal(1, handlers[0].DisposeCount);
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

  private sealed class GatedLoginHandlerInline(Task gate) : HttpMessageHandler
  {
    public int DisposeCount { get; private set; }

    public bool RequestStarted { get; private set; }

    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        DisposeCount++;
      }

      base.Dispose(disposing);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      RequestStarted = true;
      await gate.WaitAsync(cancellationToken);
      return RecordingHttpMessageHandler.Json(
        System.Text.Json.JsonSerializer.Serialize(
          new ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal.InteractiveLoginResponseDto(
            false,
            false,
            false,
            new ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal.AccessTokenResponseDto(
              "Bearer",
              "access-new",
              3600,
              "refresh-new"))));
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
  private sealed class GatedRequestHandlerInline(Task gate) : HttpMessageHandler
  {
    public int DisposeCount { get; private set; }

    public int RequestCount { get; private set; }

    public bool RequestStarted { get; private set; }

    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        DisposeCount++;
      }

      base.Dispose(disposing);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      RequestStarted = true;
      RequestCount++;
      await gate.WaitAsync(cancellationToken);
      return new HttpResponseMessage(HttpStatusCode.OK);
    }
  }
}
