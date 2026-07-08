using System.Net;
using ControlR.ApiClient;
using ControlR.ApiClient.Auth;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.TestingUtilities;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ControlR.Web.Server.Tests;

public class ControlrAuthSessionIntegrationTests(ITestOutputHelper testOutput)
{
  private static readonly IReadOnlyDictionary<string, string?> _interactiveBearerSettings = new Dictionary<string, string?>
  {
    ["AppOptions:EnableInteractiveBearerLogin"] = "true"
  };

  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task RestoreAuthSnapshot_AfterInteractiveLogin_AllowsProtectedManageInfoRequest()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: _interactiveBearerSettings);

    var (user, snapshot) = await CreateSnapshot(testServer, "restore-auth-snapshot@t.local");

    using var restoredClient = new TestControlrApiClient(testServer);
    await restoredClient.AuthSession.RestoreAuthSnapshot(snapshot);

    var manageInfoResult = await restoredClient.Api.Auth.GetManageInfo(TestContext.Current.CancellationToken);

    Assert.True(manageInfoResult.IsSuccess, manageInfoResult.ToString());
    Assert.NotNull(manageInfoResult.Value);
    Assert.Equal(user.Email, manageInfoResult.Value.Email);
    Assert.Equal(ControlrAuthSessionState.Authenticated, restoredClient.AuthSession.State);
    Assert.True(restoredClient.AuthSession.IsAuthenticated);
  }

  [Fact]
  public async Task RestoreAuthSnapshot_WhenBearerAndRefreshTokensExpire_FailsProtectedManageInfoRequestAndClearsTokens()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: _interactiveBearerSettings);

    var (_, snapshot) = await CreateSnapshot(testServer, "restore-refresh-expired@t.local");

    using var restoredClient = new TestControlrApiClient(testServer);
    await restoredClient.AuthSession.RestoreAuthSnapshot(snapshot);
    restoredClient.StopBackgroundRefresh();

    testServer.TimeProvider.Advance(TimeSpan.FromDays(31));

    var manageInfoResult = await restoredClient.Api.Auth.GetManageInfo(TestContext.Current.CancellationToken);

    Assert.False(manageInfoResult.IsSuccess);
    Assert.Equal(HttpStatusCode.Unauthorized, manageInfoResult.StatusCode);
    Assert.Null(restoredClient.AuthState.BearerToken);
    Assert.Null(restoredClient.AuthState.RefreshToken);
    Assert.Null(restoredClient.AuthState.BearerTokenExpiresAt);
  }

  [Fact]
  public async Task RestoreAuthSnapshot_WhenBearerExpiresButRefreshTokenIsStillValid_RefreshesAndAllowsProtectedManageInfoRequest()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: _interactiveBearerSettings);

    var (user, snapshot) = await CreateSnapshot(testServer, "restore-refresh-success@t.local");

    using var restoredClient = new TestControlrApiClient(testServer);
    await restoredClient.AuthSession.RestoreAuthSnapshot(snapshot);
    restoredClient.StopBackgroundRefresh();

    testServer.TimeProvider.Advance(TimeSpan.FromMinutes(61));

    var manageInfoResult = await restoredClient.Api.Auth.GetManageInfo(TestContext.Current.CancellationToken);

    Assert.True(manageInfoResult.IsSuccess, manageInfoResult.ToString());
    Assert.NotNull(manageInfoResult.Value);
    Assert.Equal(user.Email, manageInfoResult.Value.Email);
    Assert.NotNull(restoredClient.AuthState.BearerToken);
    Assert.NotNull(restoredClient.AuthState.RefreshToken);
    Assert.True(restoredClient.AuthState.BearerTokenExpiresAt > testServer.TimeProvider.GetUtcNow());
  }

  [Fact]
  public async Task RestoreAuthSnapshot_WhenSnapshotIsInvalid_ThrowsAndDoesNotAuthenticateClient()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: _interactiveBearerSettings);

    using var restoredClient = new TestControlrApiClient(testServer);

    var ex = await Assert.ThrowsAsync<ArgumentException>(() => restoredClient.AuthSession.RestoreAuthSnapshot(new AuthSnapshot(null, null, null, null)));
    var manageInfoResult = await restoredClient.Api.Auth.GetManageInfo(TestContext.Current.CancellationToken);

    Assert.Contains("bearer and refresh tokens", ex.Message);
    Assert.False(manageInfoResult.IsSuccess);
    Assert.Equal(HttpStatusCode.Unauthorized, manageInfoResult.StatusCode);
    Assert.Equal(ControlrAuthSessionState.SignedOut, restoredClient.AuthSession.State);
  }

  private async Task<(AppUser User, AuthSnapshot Snapshot)> CreateSnapshot(TestWebServer testServer, string email)
  {
    var tenant = await testServer.Services.CreateTestTenant();
    var user = await testServer.Services.CreateTestUser(tenant.Id, email);
    using var client = new TestControlrApiClient(testServer);

    var signInResult = await client.AuthSession.SignIn(
      new InteractiveSignInRequest
      {
        Email = user.Email ?? throw new InvalidOperationException("The test user must have an email address."),
        Password = "T3stP@ssw0rd!"
      },
      TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.Authenticated, signInResult.Status);

    var snapshot = client.AuthSession.GetAuthSnapshot();
    Assert.NotNull(snapshot.BearerToken);
    Assert.NotNull(snapshot.RefreshToken);
    Assert.NotNull(snapshot.BearerTokenExpiresAt);

    return (user, snapshot);
  }

  private sealed class TestControlrApiClient : IDisposable
  {
    private readonly HttpClient _apiHttpClient;
    private readonly HttpClient _unauthenticatedHttpClient;

    public TestControlrApiClient(TestWebServer testServer)
    {
      var baseUrl = new Uri("http://localhost");
      AuthState = new ControlrApiClientAuthState();

      _unauthenticatedHttpClient = new HttpClient(testServer.TestServer.CreateHandler())
      {
        BaseAddress = baseUrl
      };

      var httpClientFactory = new StaticHttpClientFactory(_unauthenticatedHttpClient);
      var options = new ControlrApiClientOptions
      {
        BaseUrl = baseUrl
      };

      var authHeaderHandler = new ControlrApiAuthHeaderHandler(AuthState)
      {
        InnerHandler = testServer.TestServer.CreateHandler()
      };

      _apiHttpClient = new HttpClient(authHeaderHandler)
      {
        BaseAddress = baseUrl
      };

      var bearerTokenRefresher = new BearerTokenRefresher(AuthState, httpClientFactory, testServer.TimeProvider);

      AuthSession = new ControlrAuthSession(
        httpClientFactory,
        AuthState,
        bearerTokenRefresher,
        NullLogger<ControlrAuthSession>.Instance,
        new OptionsMonitorWrapper<ControlrApiClientOptions>(options),
        testServer.TimeProvider);

      Api = new ControlrApi(
        _apiHttpClient,
        AuthState,
        bearerTokenRefresher,
        NullLogger<ControlrApi>.Instance,
        new OptionsWrapper<ControlrApiClientOptions>(options)).InternalApi;
    }

    public IControlrInternalApi Api { get; }
    public ControlrAuthSession AuthSession { get; }
    public ControlrApiClientAuthState AuthState { get; }

    public void Dispose()
    {
      AuthSession.Dispose();
      _apiHttpClient.Dispose();
      _unauthenticatedHttpClient.Dispose();
      GC.SuppressFinalize(this);
    }

    public void StopBackgroundRefresh()
    {
      AuthSession.Dispose();
    }
  }
}