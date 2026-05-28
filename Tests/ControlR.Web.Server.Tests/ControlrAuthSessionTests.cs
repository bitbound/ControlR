using System.Net;
using System.Net.Http.Json;
using ControlR.ApiClient;
using ControlR.ApiClient.Auth;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.TestingUtilities;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace ControlR.Web.Server.Tests;

public class ControlrAuthSessionTests
{
  [Fact]
  public async Task GetAccessToken_WhenBearerNeedsRefresh_RefreshesBeforeReturningToken()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();
    var authState = new ControlrApiClientAuthState();
    authState.SetBearerTokens("stale-token", "refresh-token", timeProvider.GetUtcNow().AddSeconds(30));

    var responseQueue = new Queue<HttpResponseMessage>([
      CreateJsonResponse(CreateTokenResponse("fresh-token", "fresh-refresh-token", expiresInSeconds: 600))
    ]);

    using var httpClient = new HttpClient(new QueueMessageHandler(responseQueue))
    {
      BaseAddress = options.BaseUrl
    };

    var session = CreateSession(options, authState, httpClient, timeProvider);

    var token = await session.GetBearerToken(TestContext.Current.CancellationToken);

    Assert.Equal("fresh-token", token);
    Assert.Equal("fresh-token", authState.BearerToken);
    Assert.Equal("fresh-refresh-token", authState.RefreshToken);
    Assert.True(session.IsAuthenticated);
    Assert.Equal(ControlrAuthSessionState.Authenticated, session.State);
  }

  [Fact]
  public async Task GetAccessToken_WhenRefreshIsCanceled_ThrowsOperationCanceledException()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();
    var authState = new ControlrApiClientAuthState();
    authState.SetBearerTokens("stale-token", "refresh-token", timeProvider.GetUtcNow().AddSeconds(30));

    using var httpClient = new HttpClient(new QueueMessageHandler([]))
    {
      BaseAddress = options.BaseUrl
    };

    var session = CreateSession(options, authState, httpClient, timeProvider);
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.GetBearerToken(cts.Token));

    Assert.Equal("stale-token", authState.BearerToken);
    Assert.Equal("refresh-token", authState.RefreshToken);
  }

  [Fact]
  public async Task GetAccessToken_WhenRefreshIsUnauthorized_ExpiresSessionAndClearsTokens()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();
    var authState = new ControlrApiClientAuthState();
    authState.SetBearerTokens("stale-token", "refresh-token", timeProvider.GetUtcNow().AddSeconds(30));

    using var httpClient = new HttpClient(new QueueMessageHandler(new Queue<HttpResponseMessage>([
      new HttpResponseMessage(HttpStatusCode.Unauthorized)
    ])))
    {
      BaseAddress = options.BaseUrl
    };

    var session = CreateSession(options, authState, httpClient, timeProvider);
    var expiredMessage = default(string?);
    session.StateChanged += (_, args) =>
    {
      if (args.State == ControlrAuthSessionState.Expired)
      {
        expiredMessage = args.Message;
      }
    };

    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => session.GetBearerToken(TestContext.Current.CancellationToken));

    Assert.Equal("The refresh token is no longer valid.", ex.Message);
    Assert.Equal(ControlrAuthSessionState.Expired, session.State);
    Assert.False(session.IsAuthenticated);
    Assert.Null(authState.BearerToken);
    Assert.Null(authState.RefreshToken);
    Assert.Null(authState.BearerTokenExpiresAt);
    Assert.Equal("The session expired. Sign in again.", expiredMessage);
  }

  [Fact]
  public async Task PreviousRefreshLoopFault_DoesNotExpireNewSession()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();
    var handler = new BlockingRefreshMessageHandler(
      [
        CreateJsonResponse(new InteractiveLoginResponseDto(
          RequiresTwoFactor: false,
          Tokens: CreateTokenResponse("first-token", "first-refresh", expiresInSeconds: 1))),
        CreateJsonResponse(new InteractiveLoginResponseDto(
          RequiresTwoFactor: false,
          Tokens: CreateTokenResponse("second-token", "second-refresh", expiresInSeconds: 300)))
      ],
      new HttpResponseMessage(HttpStatusCode.Unauthorized));

    using var httpClient = new HttpClient(handler)
    {
      BaseAddress = options.BaseUrl
    };

    var authState = new ControlrApiClientAuthState();
    var session = CreateSession(options, authState, httpClient, timeProvider);
    var expiredObserved = false;
    session.StateChanged += (_, args) =>
    {
      if (args.State == ControlrAuthSessionState.Expired)
      {
        expiredObserved = true;
      }
    };

    var firstResult = await session.SignIn(
      new InteractiveSignInRequest
      {
        Email = "viewer@example.com",
        Password = "P@ssw0rd!"
      },
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.Authenticated, firstResult.Status);

    await handler.RefreshStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

    var secondResult = await session.SignIn(
      new InteractiveSignInRequest
      {
        Email = "viewer@example.com",
        Password = "P@ssw0rd!"
      },
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.Authenticated, secondResult.Status);

    handler.ReleaseRefresh.TrySetResult();
    await handler.RefreshCompleted.Task.WaitAsync(TestContext.Current.CancellationToken);
    await Task.Delay(25, TestContext.Current.CancellationToken);

    Assert.False(expiredObserved);
    Assert.Equal(ControlrAuthSessionState.Authenticated, session.State);
    Assert.Equal("second-token", authState.BearerToken);
    Assert.Equal("second-refresh", authState.RefreshToken);
  }

  [Fact]
  public async Task RefreshLoop_WhenRefreshFails_TransitionsToExpired()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();
    var responseQueue = new Queue<HttpResponseMessage>([
      CreateJsonResponse(new InteractiveLoginResponseDto(
        RequiresTwoFactor: false,
        Tokens: CreateTokenResponse("access-token", "refresh-token", expiresInSeconds: 1))),
      new HttpResponseMessage(HttpStatusCode.Unauthorized)
    ]);

    using var httpClient = new HttpClient(new QueueMessageHandler(responseQueue))
    {
      BaseAddress = options.BaseUrl
    };

    var authState = new ControlrApiClientAuthState();
    var session = CreateSession(options, authState, httpClient, timeProvider);
    var expiredState = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

    session.StateChanged += (_, args) =>
    {
      if (args.State == ControlrAuthSessionState.Expired)
      {
        expiredState.TrySetResult(args.Message);
      }
    };

    var signInResult = await session.SignIn(
      new InteractiveSignInRequest
      {
        Email = "viewer@example.com",
        Password = "P@ssw0rd!"
      },
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.Authenticated, signInResult.Status);

    var message = await expiredState.Task.WaitAsync(TestContext.Current.CancellationToken);

    Assert.Equal(ControlrAuthSessionState.Expired, session.State);
    Assert.Equal("The session expired. Sign in again.", message);
  }

  [Fact]
  public async Task RestoreAuthSnapshot_WhenAllTokensNull_ThrowsArgumentException()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();
    var authState = new ControlrApiClientAuthState();

    using var httpClient = new HttpClient(new QueueMessageHandler([]))
    {
      BaseAddress = options.BaseUrl
    };

    var session = CreateSession(options, authState, httpClient, timeProvider);

    var snapshot = new AuthSnapshot(null, null, null, null);

    var ex = await Assert.ThrowsAsync<ArgumentException>(() => session.RestoreAuthSnapshot(snapshot));
    Assert.Contains("bearer and refresh tokens", ex.Message);
  }

  [Fact]
  public async Task RestoreAuthSnapshot_WhenBearerTokenExpiresAtIsNull_ThrowsArgumentException()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();
    var authState = new ControlrApiClientAuthState();

    using var httpClient = new HttpClient(new QueueMessageHandler([]))
    {
      BaseAddress = options.BaseUrl
    };

    var session = CreateSession(options, authState, httpClient, timeProvider);

    var snapshot = new AuthSnapshot(null, "bearer-token", null, "refresh-token");

    var ex = await Assert.ThrowsAsync<ArgumentException>(() => session.RestoreAuthSnapshot(snapshot));
    Assert.Contains("bearer and refresh tokens", ex.Message);
  }

  [Fact]
  public async Task RestoreAuthSnapshot_WhenBearerTokenIsEmpty_ThrowsArgumentException()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();
    var authState = new ControlrApiClientAuthState();

    using var httpClient = new HttpClient(new QueueMessageHandler([]))
    {
      BaseAddress = options.BaseUrl
    };

    var session = CreateSession(options, authState, httpClient, timeProvider);

    var snapshot = new AuthSnapshot(null, " ", DateTimeOffset.UtcNow.AddMinutes(5), "refresh-token");

    var ex = await Assert.ThrowsAsync<ArgumentException>(() => session.RestoreAuthSnapshot(snapshot));
    Assert.Contains("bearer and refresh tokens", ex.Message);
  }

  [Fact]
  public async Task RestoreAuthSnapshot_WhenRefreshTokenIsEmpty_ThrowsArgumentException()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();
    var authState = new ControlrApiClientAuthState();

    using var httpClient = new HttpClient(new QueueMessageHandler([]))
    {
      BaseAddress = options.BaseUrl
    };

    var session = CreateSession(options, authState, httpClient, timeProvider);

    var snapshot = new AuthSnapshot(null, "bearer-token", DateTimeOffset.UtcNow.AddMinutes(5), " ");

    var ex = await Assert.ThrowsAsync<ArgumentException>(() => session.RestoreAuthSnapshot(snapshot));
    Assert.Contains("bearer and refresh tokens", ex.Message);
  }

  [Fact]
  public async Task RestoreAuthSnapshot_WhenSnapshotHasPersonalAccessToken_RestoresPatAndSignedOutState()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();
    var authState = new ControlrApiClientAuthState();

    using var httpClient = new HttpClient(new QueueMessageHandler([]))
    {
      BaseAddress = options.BaseUrl
    };

    var session = CreateSession(options, authState, httpClient, timeProvider);

    var snapshot = new AuthSnapshot("pat-value", null, null, null);
    await session.RestoreAuthSnapshot(snapshot);

    Assert.Equal("pat-value", authState.PersonalAccessToken);
    Assert.Equal(ControlrAuthSessionState.SignedOut, session.State);
    Assert.Null(authState.BearerToken);
    Assert.Null(authState.RefreshToken);
  }

  [Fact]
  public async Task RestoreAuthSnapshot_WhenValidBearerSnapshot_RestoresTokensAndSetsAuthenticatedState()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();
    var authState = new ControlrApiClientAuthState();
    var expiresAt = timeProvider.GetUtcNow().AddMinutes(5);

    using var httpClient = new HttpClient(new QueueMessageHandler([]))
    {
      BaseAddress = options.BaseUrl
    };

    var session = CreateSession(options, authState, httpClient, timeProvider);

    var snapshot = new AuthSnapshot(null, "bearer-token", expiresAt, "refresh-token");
    await session.RestoreAuthSnapshot(snapshot);

    Assert.Equal("bearer-token", authState.BearerToken);
    Assert.Equal("refresh-token", authState.RefreshToken);
    Assert.Equal(expiresAt, authState.BearerTokenExpiresAt);
    Assert.Equal(ControlrAuthSessionState.Authenticated, session.State);
    Assert.True(session.IsAuthenticated);
  }

  [Fact]
  public async Task RoundTrip_SnapshotFromSignedInSession_RestoresIntoNewSession()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();

    var sessionAResponses = new Queue<HttpResponseMessage>([
      CreateJsonResponse(new InteractiveLoginResponseDto(
        RequiresTwoFactor: false,
        Tokens: CreateTokenResponse("session-a-bearer", "session-a-refresh", expiresInSeconds: 300)))
    ]);

    using var sessionAHttpClient = new HttpClient(new QueueMessageHandler(sessionAResponses))
    {
      BaseAddress = options.BaseUrl
    };

    var sessionAAuthState = new ControlrApiClientAuthState();
    var sessionA = CreateSession(options, sessionAAuthState, sessionAHttpClient, timeProvider);

    var signInResult = await sessionA.SignIn(
      new InteractiveSignInRequest
      {
        Email = "viewer@example.com",
        Password = "P@ssw0rd!"
      },
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.Authenticated, signInResult.Status);

    var snapshot = sessionA.GetAuthSnapshot();
    Assert.Equal("session-a-bearer", snapshot.BearerToken);
    Assert.Equal("session-a-refresh", snapshot.RefreshToken);
    Assert.NotNull(snapshot.BearerTokenExpiresAt);

    var sessionBAuthState = new ControlrApiClientAuthState();

    using var sessionBHttpClient = new HttpClient(new QueueMessageHandler([]))
    {
      BaseAddress = options.BaseUrl
    };

    var sessionB = CreateSession(options, sessionBAuthState, sessionBHttpClient, timeProvider);

    Assert.Equal(ControlrAuthSessionState.SignedOut, sessionB.State);

    await sessionB.RestoreAuthSnapshot(snapshot);

    Assert.Equal(ControlrAuthSessionState.Authenticated, sessionB.State);
    Assert.Equal("session-a-bearer", sessionBAuthState.BearerToken);
    Assert.Equal("session-a-refresh", sessionBAuthState.RefreshToken);
    Assert.Equal(snapshot.BearerTokenExpiresAt, sessionBAuthState.BearerTokenExpiresAt);

    var token = await sessionB.GetBearerToken(cancellationToken: TestContext.Current.CancellationToken);
    Assert.Equal("session-a-bearer", token);
  }

  [Fact]
  public async Task SignIn_WhenInteractiveLoginEndpointIsUnavailable_ReturnsBoundedMessage()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();

    var responseQueue = new Queue<HttpResponseMessage>([
      new HttpResponseMessage(HttpStatusCode.NotFound)
      {
        Content = JsonContent.Create(new { detail = "server detail should not leak" })
      }
    ]);

    using var httpClient = new HttpClient(new QueueMessageHandler(responseQueue))
    {
      BaseAddress = options.BaseUrl
    };

    var authState = new ControlrApiClientAuthState();
    var session = CreateSession(options, authState, httpClient, timeProvider);

    var result = await session.SignIn(
      new InteractiveSignInRequest
      {
        Email = "viewer@example.com",
        Password = "P@ssw0rd!"
      },
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.Failed, result.Status);
    Assert.Equal("Interactive login is not available on this server.", result.Message);
  }

  [Fact]
  public async Task SignIn_WhenInteractiveLoginFails_DoesNotReturnRawServerMessage()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();

    var responseQueue = new Queue<HttpResponseMessage>([
      new HttpResponseMessage(HttpStatusCode.InternalServerError)
      {
        Content = JsonContent.Create(new { detail = "sensitive server detail" })
      }
    ]);

    using var httpClient = new HttpClient(new QueueMessageHandler(responseQueue))
    {
      BaseAddress = options.BaseUrl
    };

    var authState = new ControlrApiClientAuthState();
    var session = CreateSession(options, authState, httpClient, timeProvider);

    var result = await session.SignIn(
      new InteractiveSignInRequest
      {
        Email = "viewer@example.com",
        Password = "P@ssw0rd!"
      },
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.Failed, result.Status);
    Assert.Equal("Interactive login failed.", result.Message);
  }

  [Fact]
  public async Task SignIn_WhenRecoveryCodeIsProvided_AuthenticatesSession()
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

    var authState = new ControlrApiClientAuthState();
    var session = CreateSession(options, authState, httpClient, timeProvider);

    var signInResult = await session.SignIn(
      new InteractiveSignInRequest
      {
        Email = "viewer@example.com",
        Password = "P@ssw0rd!"
      },
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.RequiresTwoFactor, signInResult.Status);

    var recoveryResult = await session.SignIn(
      new InteractiveSignInRequest
      {
        Email = "viewer@example.com",
        Password = "P@ssw0rd!",
        RecoveryCode = "recovery-code"
      },
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.Authenticated, recoveryResult.Status);
    Assert.True(session.IsAuthenticated);
    Assert.Equal(ControlrAuthSessionState.Authenticated, session.State);
    Assert.Equal("access-token", authState.BearerToken);
  }

  [Fact]
  public async Task SignIn_WhenSecondAttemptProvidesTwoFactorCode_AuthenticatesWithoutStoredCredentials()
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

    var authState = new ControlrApiClientAuthState();
    var session = CreateSession(options, authState, httpClient, timeProvider);

    var signInResult = await session.SignIn(
      new InteractiveSignInRequest
      {
        Email = "viewer@example.com",
        Password = "P@ssw0rd!"
      },
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.RequiresTwoFactor, signInResult.Status);

    var result = await session.SignIn(
      new InteractiveSignInRequest
      {
        Email = "viewer@example.com",
        Password = "P@ssw0rd!",
        TwoFactorCode = "123456"
      },
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.Authenticated, result.Status);
    Assert.Equal("Connected.", result.Message);
    Assert.Equal(ControlrAuthSessionState.Authenticated, session.State);
    Assert.False(session.RequiresTwoFactor);
    Assert.True(session.IsAuthenticated);
    Assert.Equal("access-token", authState.BearerToken);
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

    var authState = new ControlrApiClientAuthState();
    var session = CreateSession(options, authState, httpClient, timeProvider);
    var observedStates = new List<ControlrAuthSessionState>();
    session.StateChanged += (_, args) => observedStates.Add(args.State);

    var signInResult = await session.SignIn(
      new InteractiveSignInRequest
      {
        Email = "viewer@example.com",
        Password = "P@ssw0rd!"
      },
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.RequiresTwoFactor, signInResult.Status);
    Assert.True(session.RequiresTwoFactor);
    Assert.Equal(ControlrAuthSessionState.AwaitingTwoFactor, session.State);

    var twoFactorResult = await session.SignIn(
      new InteractiveSignInRequest
      {
        Email = "viewer@example.com",
        Password = "P@ssw0rd!",
        TwoFactorCode = "123456"
      },
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.Authenticated, twoFactorResult.Status);
    Assert.True(session.IsAuthenticated);
    Assert.Equal(ControlrAuthSessionState.Authenticated, session.State);
    Assert.Equal("access-token", authState.BearerToken);
    Assert.Equal("refresh-token", authState.RefreshToken);
    Assert.Equal(
      [ControlrAuthSessionState.AwaitingTwoFactor, ControlrAuthSessionState.Authenticated],
      observedStates);
  }

  [Fact]
  public async Task SignOut_ClearsTokensAndReturnsToSignedOutState()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();
    var authState = new ControlrApiClientAuthState();
    authState.SetBearerTokens("access-token", "refresh-token", timeProvider.GetUtcNow().AddMinutes(5));

    using var httpClient = new HttpClient(new QueueMessageHandler([]))
    {
      BaseAddress = options.BaseUrl
    };

    var session = CreateSession(options, authState, httpClient, timeProvider);

    await session.SignOut();

    Assert.False(session.IsAuthenticated);
    Assert.Equal(ControlrAuthSessionState.SignedOut, session.State);
    Assert.Null(authState.BearerToken);
    Assert.Null(authState.RefreshToken);
    Assert.Null(authState.BearerTokenExpiresAt);
  }

  [Fact]
  public async Task SignOut_WhenRefreshCompletesLate_DoesNotRestoreBearerTokens()
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var options = CreateOptions();
    var handler = new BlockingRefreshMessageHandler(
      [CreateJsonResponse(new InteractiveLoginResponseDto(
        RequiresTwoFactor: false,
        Tokens: CreateTokenResponse("access-token", "refresh-token", expiresInSeconds: 1)))],
      CreateJsonResponse(CreateTokenResponse("late-token", "late-refresh", expiresInSeconds: 600)));

    using var httpClient = new HttpClient(handler)
    {
      BaseAddress = options.BaseUrl
    };

    var authState = new ControlrApiClientAuthState();
    var session = CreateSession(options, authState, httpClient, timeProvider);

    var signInResult = await session.SignIn(
      new InteractiveSignInRequest
      {
        Email = "viewer@example.com",
        Password = "P@ssw0rd!"
      },
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.Authenticated, signInResult.Status);

    await handler.RefreshStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

    await session.SignOut();
    handler.ReleaseRefresh.TrySetResult();
    await handler.RefreshCompleted.Task.WaitAsync(TestContext.Current.CancellationToken);
    await Task.Delay(25, TestContext.Current.CancellationToken);

    Assert.Equal(ControlrAuthSessionState.SignedOut, session.State);
    Assert.Null(authState.BearerToken);
    Assert.Null(authState.RefreshToken);
    Assert.Null(authState.BearerTokenExpiresAt);
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

  private static ControlrAuthSession CreateSession(
    ControlrApiClientOptions options,
    ControlrApiClientAuthState authState,
    HttpClient httpClient,
    FakeTimeProvider timeProvider)
  {
    var httpClientFactory = new StaticHttpClientFactory(httpClient);

    return new ControlrAuthSession(
      httpClientFactory,
      authState,
      new BearerTokenRefresher(authState, httpClientFactory, timeProvider),
      NullLogger<ControlrAuthSession>.Instance,
      new OptionsMonitorWrapper<ControlrApiClientOptions>(options),
      timeProvider);
  }

  private static AccessTokenResponseDto CreateTokenResponse(string accessToken, string refreshToken, int expiresInSeconds)
  {
    return new AccessTokenResponseDto(
      TokenType: "Bearer",
      AccessToken: accessToken,
      ExpiresInSeconds: expiresInSeconds,
      RefreshToken: refreshToken);
  }

  private sealed class BlockingRefreshMessageHandler(
    IEnumerable<HttpResponseMessage> interactiveLoginResponses,
    HttpResponseMessage refreshResponse) : HttpMessageHandler
  {
    private readonly Queue<HttpResponseMessage> _interactiveLoginResponses = new(interactiveLoginResponses);
    private readonly HttpResponseMessage _refreshResponse = refreshResponse;

    public TaskCompletionSource RefreshCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource RefreshStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseRefresh { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var path = request.RequestUri?.AbsolutePath;
      if (string.Equals(path, "/api/auth/interactive-login", StringComparison.Ordinal))
      {
        if (_interactiveLoginResponses.Count == 0)
        {
          throw new InvalidOperationException("No queued interactive-login response was available.");
        }

        return _interactiveLoginResponses.Dequeue();
      }

      if (string.Equals(path, "/api/auth/refresh", StringComparison.Ordinal))
      {
        RefreshStarted.TrySetResult();
        await ReleaseRefresh.Task.WaitAsync(TestContext.Current.CancellationToken);
        RefreshCompleted.TrySetResult();
        return _refreshResponse;
      }

      throw new InvalidOperationException($"Unexpected request path: {path}");
    }
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
}