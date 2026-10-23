using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlR.ApiClient.Auth;
using ControlR.ApiClient.Tests.Helpers;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace ControlR.ApiClient.Tests;

/// <summary>
/// Covers on-demand bearer renewal through a factory target, with a scripted fake server.
/// </summary>
public sealed class ControlrAuthSessionTests
{
  private const string ExpiredAccessToken = "access-expired";

  private static readonly Uri _server = new("https://server.test/");
  private static readonly TimeSpan _testTimeout = TimeSpan.FromSeconds(10);

  [Fact]
  public async Task ApiCall_WhenManyCallsGetUnauthorizedAtOnce_SharesOneRefreshAndRetriesEach()
  {
    var server = new FakeAuthServer { RejectedAccessToken = ExpiredAccessToken };
    using var fixture = CreateTarget(server, new FakeTimeProvider());
    await fixture.Session.RestoreAuthSnapshot(Snapshot(ExpiredAccessToken, fixture.Time, TimeSpan.FromHours(1)));

    var calls = Enumerable.Range(0, 20)
      .Select(_ => fixture.Api.Internal.Devices.DeleteDevice(Guid.NewGuid(), TestContext.Current.CancellationToken))
      .ToArray();
    var results = await Task.WhenAll(calls).WaitAsync(_testTimeout, TestContext.Current.CancellationToken);

    Assert.All(results, result => Assert.True(result.IsSuccess, result.Reason));
    Assert.Equal(1, server.RefreshCount);
  }

  [Fact]
  public async Task ApiCall_WhenRefreshTokenIsRejected_ExpiresSessionAndStopsRefreshing()
  {
    var server = new FakeAuthServer
    {
      RejectedAccessToken = ExpiredAccessToken,
      RejectRefresh = true
    };
    using var fixture = CreateTarget(server, new FakeTimeProvider());
    await fixture.Session.RestoreAuthSnapshot(Snapshot(ExpiredAccessToken, fixture.Time, TimeSpan.FromHours(1)));

    var states = new List<ControlrAuthSessionState>();
    fixture.Session.StateChanged += (_, e) => states.Add(e.State);

    var first = await fixture.Api.Internal.Devices.DeleteDevice(Guid.NewGuid(), TestContext.Current.CancellationToken);
    var second = await fixture.Api.Internal.Devices.DeleteDevice(Guid.NewGuid(), TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    Assert.Equal(1, server.RefreshCount);
    Assert.Equal(ControlrAuthSessionState.Expired, fixture.Session.State);
    Assert.Equal([ControlrAuthSessionState.Expired], states);
    Assert.Null(fixture.Session.GetAuthSnapshot().BearerToken);
  }

  [Fact]
  public async Task ApiCall_WhenServerLifetimeIsShorterThanLeadTime_DoesNotRefreshOnEveryCall()
  {
    var server = new FakeAuthServer { IssuedLifetime = TimeSpan.FromSeconds(30) };
    var time = new FakeTimeProvider();
    using var fixture = CreateTarget(server, time);
    await SignIn(fixture.Session);

    for (var i = 0; i < 10; i++)
    {
      await fixture.Api.Internal.Devices.DeleteDevice(Guid.NewGuid(), TestContext.Current.CancellationToken);
    }

    // The default lead time is a minute, so without the half-lifetime cap every fresh 30 second
    // token would already be due and each call would post a refresh.
    Assert.Equal(0, server.RefreshCount);

    time.Advance(TimeSpan.FromSeconds(16));
    for (var i = 0; i < 10; i++)
    {
      await fixture.Api.Internal.Devices.DeleteDevice(Guid.NewGuid(), TestContext.Current.CancellationToken);
    }

    Assert.Equal(1, server.RefreshCount);
  }

  [Fact]
  public async Task ApiCall_WhenTokenIsNearExpiry_RenewsBeforeSending()
  {
    var server = new FakeAuthServer();
    var time = new FakeTimeProvider();
    using var fixture = CreateTarget(server, time);
    await SignIn(fixture.Session);
    var signedInToken = fixture.Session.GetAuthSnapshot().BearerToken;

    time.Advance(server.IssuedLifetime - TimeSpan.FromSeconds(30));
    var result = await fixture.Api.Internal.Devices.DeleteDevice(Guid.NewGuid(), TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess, result.Reason);
    Assert.Equal(1, server.RefreshCount);
    Assert.NotEqual(signedInToken, server.LastApiAccessToken);
    Assert.Equal(fixture.Session.GetAuthSnapshot().BearerToken, server.LastApiAccessToken);
  }

  [Fact]
  public async Task GetBearerToken_WhenCallerCancelsDuringSlowRefresh_ReturnsPromptlyAndRefreshStillLands()
  {
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var server = new FakeAuthServer { RefreshGate = gate.Task };
    var time = new FakeTimeProvider();
    using var fixture = CreateTarget(server, time);
    await fixture.Session.RestoreAuthSnapshot(Snapshot("access-old", time, TimeSpan.FromSeconds(10)));

    using var cts = new CancellationTokenSource();
    var cancelled = fixture.Session.GetBearerToken(cts.Token);
    var waiting = fixture.Session.GetBearerToken(TestContext.Current.CancellationToken);
    cts.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.WaitAsync(_testTimeout, TestContext.Current.CancellationToken));

    gate.SetResult();
    var token = await waiting.WaitAsync(_testTimeout, TestContext.Current.CancellationToken);

    Assert.Equal(1, server.RefreshCount);
    Assert.NotEqual("access-old", token);
    Assert.Equal(token, fixture.Session.GetAuthSnapshot().BearerToken);
  }

  [Fact]
  public async Task SignIn_WhenTwoFactorIsRequired_ThenCodeIsSupplied_Authenticates()
  {
    var server = new FakeAuthServer { RequireTwoFactor = true };
    using var fixture = CreateTarget(server, new FakeTimeProvider());

    var first = await fixture.Session.SignIn(
      new InteractiveSignInRequest { Email = "user@test.test", Password = "password" },
      TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.RequiresTwoFactor, first.Status);
    Assert.True(fixture.Session.RequiresTwoFactor);

    var second = await fixture.Session.SignIn(
      new InteractiveSignInRequest { Email = "user@test.test", Password = "password", TwoFactorCode = "123456" },
      TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.Authenticated, second.Status);
    Assert.True(fixture.Session.IsAuthenticated);
  }

  [Fact]
  public async Task SignOut_WhileRefreshIsInFlight_RefreshDoesNotRestoreTheToken()
  {
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var server = new FakeAuthServer { RefreshGate = gate.Task };
    var time = new FakeTimeProvider();
    using var fixture = CreateTarget(server, time);
    await fixture.Session.RestoreAuthSnapshot(Snapshot("access-old", time, TimeSpan.FromSeconds(10)));

    var pending = fixture.Session.GetBearerToken(TestContext.Current.CancellationToken);
    await fixture.Session.SignOut();
    gate.SetResult();
    await pending.WaitAsync(_testTimeout, TestContext.Current.CancellationToken);

    Assert.Null(fixture.Session.GetAuthSnapshot().BearerToken);
    Assert.Equal(ControlrAuthSessionState.SignedOut, fixture.Session.State);
  }

  private static TargetFixture CreateTarget(FakeAuthServer server, FakeTimeProvider time)
  {
    var factory = new ControlrApiClientFactory(
      new ControlrApiClientFactoryOptions
      {
        MaxIdleClientLifetime = null,
        HttpMessageHandlerFactory = () => new FakeServerHandler(server)
      },
      time,
      NullLoggerFactory.Instance);

    var api = factory.GetOrCreateClient("a", o => o.BaseUrl = _server);
    return new TargetFixture(factory, api, factory.GetOrCreateAuthSession("a"), time);
  }

  private static async Task SignIn(IControlrAuthSession session)
  {
    var result = await session.SignIn(
      new InteractiveSignInRequest { Email = "user@test.test", Password = "password" },
      TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.Authenticated, result.Status);
  }

  private static AuthSnapshot Snapshot(string accessToken, TimeProvider time, TimeSpan expiresIn) =>
    new(null, accessToken, time.GetUtcNow() + expiresIn, "refresh-initial");

  private sealed record TargetFixture(
    ControlrApiClientFactory Factory,
    IControlrApi Api,
    IControlrAuthSession Session,
    FakeTimeProvider Time) : IDisposable
  {
    public void Dispose() => Factory.Dispose();
  }

  /// <summary>
  /// A minimal stand-in for the auth and device endpoints. Device calls answer 401 to
  /// <see cref="RejectedAccessToken"/> and 200 to any other bearer token.
  /// </summary>
  private sealed class FakeAuthServer
  {
    private int _issued;
    private int _refreshCount;

    public TimeSpan IssuedLifetime { get; init; } = TimeSpan.FromHours(1);
    public string? LastApiAccessToken { get; private set; }
    public int RefreshCount => Volatile.Read(ref _refreshCount);
    public Task? RefreshGate { get; init; }
    public string? RejectedAccessToken { get; init; }
    public bool RejectRefresh { get; init; }
    public bool RequireTwoFactor { get; init; }

    public async Task<HttpResponseMessage> Respond(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var path = request.RequestUri!.AbsolutePath;

      if (path.EndsWith("/interactive-login", StringComparison.Ordinal))
      {
        var login = await request.Content!.ReadFromJsonAsync<LoginRequestDto>(cancellationToken);
        if (RequireTwoFactor && login?.TwoFactorCode is null)
        {
          return Json(new InteractiveLoginResponseDto(RequiresTwoFactor: true));
        }

        return Json(new InteractiveLoginResponseDto(RequiresTwoFactor: false, Tokens: IssueTokens()));
      }

      if (path.EndsWith("/refresh", StringComparison.Ordinal))
      {
        Interlocked.Increment(ref _refreshCount);
        if (RefreshGate is not null)
        {
          await RefreshGate.WaitAsync(cancellationToken);
        }

        return RejectRefresh
          ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
          : Json(IssueTokens());
      }

      var accessToken = request.Headers.Authorization?.Parameter;
      LastApiAccessToken = accessToken;

      return accessToken is null || accessToken == RejectedAccessToken
        ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
        : new HttpResponseMessage(HttpStatusCode.OK);
    }

    private static HttpResponseMessage Json<T>(T payload) => RecordingHttpMessageHandler.Json(JsonSerializer.Serialize(payload));

    private AccessTokenResponseDto IssueTokens()
    {
      var id = Interlocked.Increment(ref _issued);
      return new AccessTokenResponseDto("Bearer", $"access-{id}", (int)IssuedLifetime.TotalSeconds, $"refresh-{id}");
    }
  }

  private sealed class FakeServerHandler(FakeAuthServer server) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
      server.Respond(request, cancellationToken);
  }
}
