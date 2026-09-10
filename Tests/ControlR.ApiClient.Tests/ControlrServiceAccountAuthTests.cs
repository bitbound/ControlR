using ControlR.ApiClient.Auth;
using ControlR.ApiClient.Internal;
using ControlR.ApiClient.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace ControlR.ApiClient.Tests;

/// <summary>
///   Covers the service account credential as a third authentication mechanism, and the precedence
///   between it, a personal access token, and an interactive bearer token.
/// </summary>
public sealed class ControlrServiceAccountAuthTests
{
  private const string ApiKey = "0123456789ABCDEF0123456789ABCDEF0123:secret-value";

  private static readonly Uri _server = new("https://server.test/");

  [Fact]
  public async Task ApplyAuthHeader_WhenApiKeySeededOnDefaultRequestHeadersAndStateIsEmpty_RemovesIt()
  {
    var recorder = new RecordingHttpMessageHandler();
    var authState = new ControlrApiClientAuthState();
    using var client = new HttpClient(
      new ControlrApiAuthHeaderHandler(authState) { InnerHandler = recorder })
    {
      BaseAddress = _server
    };

    // A caller that puts a key on DefaultRequestHeaders keeps it on every outgoing request unless the
    // auth handler scrubs it, which would leak the credential alongside the state's real choice.
    client.DefaultRequestHeaders.Add(ControlrApiClientOptions.ServiceAccountApiKeyHeader, "seeded-key");

    using var request = new HttpRequestMessage(HttpMethod.Get, _server);
    using (await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken))
    {
    }

    var sent = Assert.Single(recorder.Requests);
    Assert.False(sent.Headers.Contains(ControlrApiClientOptions.ServiceAccountApiKeyHeader));
  }

  [Fact]
  public void AuthSnapshot_WhenConstructedWithFourArguments_LeavesApiKeyNull()
  {
    // The credential is a trailing optional positional member, so every pre-existing construction of
    // a snapshot stays source compatible.
    var snapshot = new AuthSnapshot("pat", "access", DateTimeOffset.UnixEpoch, "refresh");

    Assert.Null(snapshot.ServiceAccountApiKey);
  }

  [Fact]
  public async Task GetOrCreateClient_WhenApiKeyConfigured_SendsApiKeyHeader()
  {
    RecordingHttpMessageHandler? authRecorder = null;
    var factoryOptions = new ControlrApiClientFactoryOptions { MaxIdleClientLifetime = null };
    factoryOptions.HttpMessageHandlerFactory = () =>
    {
      var handler = new RecordingHttpMessageHandler();
      authRecorder ??= handler;
      return handler;
    };

    using var factory = new ControlrApiClientFactory(
      factoryOptions,
      TimeProvider.System,
      NullLoggerFactory.Instance);

    var client = factory.GetOrCreateClient("a", o =>
    {
      o.BaseUrl = _server;
      o.ServiceAccountApiKey = ApiKey;
    });

    await client.Internal.Devices.DeleteDevice(Guid.NewGuid(), TestContext.Current.CancellationToken);

    var request = Assert.Single(authRecorder!.Requests);
    Assert.Equal(
      ApiKey,
      request.Headers.GetValues(ControlrApiClientOptions.ServiceAccountApiKeyHeader).Single());
    Assert.False(request.Headers.Contains(ControlrApiClientOptions.PersonalAccessTokenHeader));
  }

  [Fact]
  public async Task RestoreAuthSnapshot_WhenSnapshotHasNoCredential_ThrowsArgument()
  {
    using var session = CreateSession(new FakeTimeProvider());

    await Assert.ThrowsAsync<ArgumentException>(
      () => session.RestoreAuthSnapshot(new AuthSnapshot(null, null, null, null)));
  }

  [Fact]
  public async Task RestoreAuthSnapshot_WhenSnapshotHasOnlyApiKey_RestoresServiceAccountState()
  {
    using var session = CreateSession(new FakeTimeProvider());

    // Without this branch the round trip of a key-only snapshot threw, because the bearer
    // validation treated a configured credential as an incomplete token set.
    await session.RestoreAuthSnapshot(new AuthSnapshot(null, null, null, null, ApiKey));

    Assert.Equal(ControlrAuthSessionState.ServiceAccountConfigured, session.State);
    Assert.Equal(ApiKey, session.ServiceAccountApiKey);
  }

  [Fact]
  public void SetPersonalAccessToken_WhenApiKeyConfigured_ClearsApiKey()
  {
    using var session = CreateSession(new FakeTimeProvider());
    session.SetServiceAccountApiKey(ApiKey);

    session.SetPersonalAccessToken("pat-value");

    var snapshot = session.GetAuthSnapshot();
    Assert.Null(snapshot.ServiceAccountApiKey);
    Assert.Equal("pat-value", snapshot.PersonalAccessToken);
  }

  [Fact]
  public async Task SetServiceAccountApiKey_WhenBearerSessionActive_ClearsBearerTokens()
  {
    var timeProvider = new FakeTimeProvider();
    using var session = CreateSession(timeProvider);

    // The expiry must come from the same clock the refresh loop runs on. A real-now expiry against a
    // fake clock whose now is years earlier yields a delay the loop cannot schedule.
    await session.RestoreAuthSnapshot(
      new AuthSnapshot(null, "access", timeProvider.GetUtcNow().AddMinutes(30), "refresh"));
    Assert.Equal(ControlrAuthSessionState.Authenticated, session.State);

    session.SetServiceAccountApiKey(ApiKey);

    var snapshot = session.GetAuthSnapshot();
    Assert.Null(snapshot.BearerToken);
    Assert.Null(snapshot.RefreshToken);
    Assert.Equal(ApiKey, snapshot.ServiceAccountApiKey);
    Assert.Equal(ControlrAuthSessionState.ServiceAccountConfigured, session.State);
  }

  [Fact]
  public void TryGetAuthHeader_WhenApiKeyAndBearerTokenSet_ReturnsBearer()
  {
    var authState = new ControlrApiClientAuthState(serviceAccountApiKey: ApiKey);
    authState.SetBearerTokens("access", "refresh", DateTimeOffset.UtcNow.AddMinutes(30));

    Assert.True(authState.TryGetAuthHeader(out var headerName, out var headerValue));

    Assert.Equal(ControlrApiClientAuthState.AuthorizationHeader, headerName);
    Assert.Equal("Bearer access", headerValue);
  }

  [Fact]
  public void TryGetAuthHeader_WhenApiKeyAndPersonalAccessTokenSet_ReturnsPersonalAccessToken()
  {
    var authState = new ControlrApiClientAuthState("pat-value", ApiKey);

    Assert.True(authState.TryGetAuthHeader(out var headerName, out _));

    Assert.Equal(ControlrApiClientOptions.PersonalAccessTokenHeader, headerName);
  }

  [Fact]
  public void TryGetAuthHeader_WhenOnlyServiceAccountApiKeySet_ReturnsApiKeyHeader()
  {
    var authState = new ControlrApiClientAuthState(serviceAccountApiKey: ApiKey);

    Assert.True(authState.TryGetAuthHeader(out var headerName, out var headerValue));

    // The server's scheme selector keys off this exact name, so a typo authenticates as a cookie.
    Assert.Equal("x-api-key", ControlrApiClientOptions.ServiceAccountApiKeyHeader);
    Assert.Equal(ControlrApiClientOptions.ServiceAccountApiKeyHeader, headerName);
    Assert.Equal(ApiKey, headerValue);
  }

  private static ControlrAuthSession CreateSession(FakeTimeProvider timeProvider)
  {
    var options = new ControlrApiClientOptions { BaseUrl = _server };
    var handler = new RecordingHttpMessageHandler();
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
}
