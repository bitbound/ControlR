using System.Net;
using System.Net.Http.Json;
using ControlR.ApiClient;
using ControlR.ApiClient.Auth;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ControlR.Web.Server.Tests;

public class ControlrAuthSessionTests
{
  [Fact]
  public async Task GetAccessToken_WhenBearerNeedsRefresh_RefreshesBeforeReturningToken()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();
    options.Auth.BearerToken = "stale-token";
    options.Auth.RefreshToken = "refresh-token";
    options.Auth.BearerTokenExpiresAt = timeProvider.GetUtcNow().AddSeconds(30);

    var responseQueue = new Queue<HttpResponseMessage>([
      CreateJsonResponse(CreateTokenResponse("fresh-token", "fresh-refresh-token", expiresInSeconds: 600))
    ]);

    using var httpClient = new HttpClient(new QueueMessageHandler(responseQueue))
    {
      BaseAddress = options.BaseUrl
    };

    var session = CreateSession(options, httpClient, timeProvider);

    var token = await session.GetAccessToken();

    Assert.Equal("fresh-token", token);
    Assert.Equal("fresh-token", options.Auth.BearerToken);
    Assert.Equal("fresh-refresh-token", options.Auth.RefreshToken);
    Assert.True(session.IsAuthenticated);
    Assert.Equal(ControlrAuthSessionState.Authenticated, session.State);
  }

  [Fact]
  public async Task SignIn_WhenTwoFactorIsRequired_SetsAwaitingTwoFactorState()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();
    var responseQueue = new Queue<HttpResponseMessage>([
      CreateJsonResponse(new InteractiveLoginResponseDto(RequiresTwoFactor: true)),
      CreateJsonResponse(new InteractiveLoginResponseDto(
        RequiresTwoFactor: false,
        Tokens: CreateTokenResponse("access-token", "refresh-token", expiresInSeconds: 300)))
    ]);

    using var httpClient = new HttpClient(new QueueMessageHandler(responseQueue))
    {
      BaseAddress = options.BaseUrl
    };

    var session = CreateSession(options, httpClient, timeProvider);
    var observedStates = new List<ControlrAuthSessionState>();
    session.StateChanged += (_, args) => observedStates.Add(args.State);

    var signInResult = await session.SignIn(
      "viewer@example.com",
      "P@ssw0rd!",
      TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.RequiresTwoFactor, signInResult.Status);
    Assert.True(session.RequiresTwoFactor);
    Assert.Equal(ControlrAuthSessionState.AwaitingTwoFactor, session.State);

    var twoFactorResult = await session.SubmitTwoFactorCode(
      "123456",
      TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.Authenticated, twoFactorResult.Status);
    Assert.True(session.IsAuthenticated);
    Assert.Equal(ControlrAuthSessionState.Authenticated, session.State);
    Assert.Equal("access-token", options.Auth.BearerToken);
    Assert.Equal("refresh-token", options.Auth.RefreshToken);
    Assert.Equal(
      [ControlrAuthSessionState.AwaitingTwoFactor, ControlrAuthSessionState.Authenticated],
      observedStates);
  }

  [Fact]
  public async Task SignOut_ClearsTokensAndReturnsToSignedOutState()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();
    options.Auth.BearerToken = "access-token";
    options.Auth.RefreshToken = "refresh-token";
    options.Auth.BearerTokenExpiresAt = timeProvider.GetUtcNow().AddMinutes(5);

    using var httpClient = new HttpClient(new QueueMessageHandler([]))
    {
      BaseAddress = options.BaseUrl
    };

    var session = CreateSession(options, httpClient, timeProvider);

    await session.SignOut();

    Assert.False(session.IsAuthenticated);
    Assert.Equal(ControlrAuthSessionState.SignedOut, session.State);
    Assert.Null(options.Auth.BearerToken);
    Assert.Null(options.Auth.RefreshToken);
    Assert.Null(options.Auth.BearerTokenExpiresAt);
  }

  private static HttpResponseMessage CreateJsonResponse<T>(T payload)
  {
    return new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = JsonContent.Create(payload)
    };
  }

  private static ControlrApiClientOptions CreateOptions()
  {
    return new ControlrApiClientOptions
    {
      BaseUrl = new Uri("https://controlr.example.com")
    };
  }

  private static ControlrAuthSession CreateSession(ControlrApiClientOptions options, HttpClient httpClient, FakeTimeProvider timeProvider)
  {
    return new ControlrAuthSession(
      new StaticHttpClientFactory(httpClient),
      NullLogger<ControlrAuthSession>.Instance,
      new StaticOptionsMonitor<ControlrApiClientOptions>(options),
      timeProvider);
  }

  private static AccessTokenResponseDto CreateTokenResponse(string accessToken, string refreshToken, int expiresInSeconds)
  {
    return new AccessTokenResponseDto(
      TokenType: "Bearer",
      AccessToken: accessToken,
      ExpiresIn: expiresInSeconds,
      RefreshToken: refreshToken);
  }

  private sealed class QueueMessageHandler(Queue<HttpResponseMessage> responses) : HttpMessageHandler
  {
    private readonly Queue<HttpResponseMessage> _responses = responses;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      if (_responses.Count == 0)
      {
        throw new InvalidOperationException($"No queued response was available for {request.Method} {request.RequestUri}.");
      }

      return Task.FromResult(_responses.Dequeue());
    }
  }
  private sealed class StaticHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
  {
    private readonly HttpClient _httpClient = httpClient;

    public HttpClient CreateClient(string name) => _httpClient;
  }
  private sealed class StaticOptionsMonitor<TOptions>(TOptions currentValue) : IOptionsMonitor<TOptions>
  {
    private readonly TOptions _currentValue = currentValue;

    public TOptions CurrentValue => _currentValue;

    public TOptions Get(string? name) => _currentValue;

    public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
  }
}