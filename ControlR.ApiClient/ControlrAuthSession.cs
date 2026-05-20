using System.Net;
using System.Net.Http.Json;
using ControlR.ApiClient.Auth;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.ApiClient;

public interface IControlrAuthSession : IDisposable
{
  event EventHandler<ControlrAuthSessionStateChangedEventArgs>? StateChanged;

  DateTimeOffset? AccessTokenExpiresAt { get; }
  Uri BaseUrl { get; }
  bool IsAuthenticated { get; }
  string? PersonalAccessToken { get; }
  bool RequiresTwoFactor { get; }
  ControlrAuthSessionState State { get; }

  Task<string?> GetAccessToken(CancellationToken cancellationToken = default);
  void SetBaseUrl(Uri baseUrl);
  void SetPersonalAccessToken(string? personalAccessToken);
  Task<InteractiveLoginResult> SignIn(string email, string password, CancellationToken cancellationToken = default);
  Task SignOut();
  Task<InteractiveLoginResult> SubmitRecoveryCode(string recoveryCode, CancellationToken cancellationToken = default);
  Task<InteractiveLoginResult> SubmitTwoFactorCode(string twoFactorCode, CancellationToken cancellationToken = default);
}

public sealed class ControlrAuthSession(
  IHttpClientFactory httpClientFactory,
  ControlrApiClientAuthState authState,
  IBearerTokenRefresher bearerTokenRefresher,
  ILogger<ControlrAuthSession> logger,
  IOptionsMonitor<ControlrApiClientOptions> optionsMonitor,
  TimeProvider timeProvider) : IControlrAuthSession
{
  private const string InteractiveLoginEndpoint = $"{HttpConstants.AuthEndpoint}/interactive-login";

  private static readonly TimeSpan _refreshLeadTime = TimeSpan.FromMinutes(1);

  private readonly ControlrApiClientAuthState _authState = authState;
  private readonly IBearerTokenRefresher _bearerTokenRefresher = bearerTokenRefresher;
  private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
  private readonly ILogger<ControlrAuthSession> _logger = logger;
  private readonly TimeProvider _timeProvider = timeProvider;

  private Uri _baseUrl = optionsMonitor.CurrentValue.BaseUrl;
  private string? _pendingEmail;
  private string? _pendingPassword;
  private CancellationTokenSource? _refreshLoopCts;
  private long _refreshLoopGeneration;
  private ControlrAuthSessionState _state = ControlrAuthSessionState.SignedOut;

  public event EventHandler<ControlrAuthSessionStateChangedEventArgs>? StateChanged;

  public DateTimeOffset? AccessTokenExpiresAt => _authState.BearerTokenExpiresAt;
  public Uri BaseUrl => Volatile.Read(ref _baseUrl);
  public bool IsAuthenticated => State == ControlrAuthSessionState.Authenticated;
  public string? PersonalAccessToken => _authState.PersonalAccessToken;
  public bool RequiresTwoFactor => State == ControlrAuthSessionState.AwaitingTwoFactor;
  public ControlrAuthSessionState State => _state;

  public void Dispose()
  {
    StopRefreshLoop();
  }

  public async Task<string?> GetAccessToken(CancellationToken cancellationToken = default)
  {
    if (!string.IsNullOrWhiteSpace(_authState.PersonalAccessToken))
    {
      return null;
    }

    try
    {
      await RefreshBearerTokenIfNeeded(forceRefresh: false, cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to refresh bearer token before providing access token.");
    }

    return _authState.BearerToken;
  }

  public void SetBaseUrl(Uri baseUrl)
  {
    Volatile.Write(ref _baseUrl, baseUrl);
  }

  public void SetPersonalAccessToken(string? personalAccessToken)
  {
    ResetSession(clearPersonalAccessToken: true);

    if (!string.IsNullOrWhiteSpace(personalAccessToken))
    {
      _authState.SetPersonalAccessToken(personalAccessToken);
    }

    UpdateState(ControlrAuthSessionState.SignedOut);
  }

  public async Task<InteractiveLoginResult> SignIn(string email, string password, CancellationToken cancellationToken = default)
  {
    var result = await ExecuteInteractiveLogin(
      new LoginRequestDto(email, password),
      cancellationToken);

    if (result.Status == InteractiveLoginStatus.RequiresTwoFactor)
    {
      _pendingEmail = email;
      _pendingPassword = password;
      UpdateState(ControlrAuthSessionState.AwaitingTwoFactor);
      return result with { Message = "Two-factor authentication is enabled. Enter your authenticator code to continue." };
    }

    if (result.Status == InteractiveLoginStatus.Authenticated)
    {
      ClearPendingCredentials();
      return result with { Message = "Connected." };
    }

    return result.Message is null
      ? result with { Message = "Email or password was not accepted." }
      : result;
  }

  public Task SignOut()
  {
    ResetSession(clearPersonalAccessToken: true);
    UpdateState(ControlrAuthSessionState.SignedOut);
    return Task.CompletedTask;
  }

  public async Task<InteractiveLoginResult> SubmitRecoveryCode(string recoveryCode, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(_pendingEmail) || string.IsNullOrWhiteSpace(_pendingPassword))
    {
      return new InteractiveLoginResult(InteractiveLoginStatus.Failed, "A login attempt is not waiting for two-factor authentication.");
    }

    var result = await ExecuteInteractiveLogin(
      new LoginRequestDto(_pendingEmail, _pendingPassword, TwoFactorCode: null, TwoFactorRecoveryCode: recoveryCode),
      cancellationToken);

    if (result.Status == InteractiveLoginStatus.Authenticated)
    {
      ClearPendingCredentials();
      return result with { Message = "Connected." };
    }

    return result.Message is null
      ? result with { Message = "The recovery code was rejected." }
      : result;
  }

  public async Task<InteractiveLoginResult> SubmitTwoFactorCode(string twoFactorCode, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(_pendingEmail) || string.IsNullOrWhiteSpace(_pendingPassword))
    {
      return new InteractiveLoginResult(InteractiveLoginStatus.Failed, "A login attempt is not waiting for two-factor authentication.");
    }

    var result = await ExecuteInteractiveLogin(
      new LoginRequestDto(_pendingEmail, _pendingPassword, TwoFactorCode: twoFactorCode, TwoFactorRecoveryCode: null),
      cancellationToken);

    if (result.Status == InteractiveLoginStatus.Authenticated)
    {
      ClearPendingCredentials();
      return result with { Message = "Connected." };
    }

    return result.Message is null
      ? result with { Message = "The two-factor code was rejected." }
      : result;
  }

  private static void CancelRefreshLoop(CancellationTokenSource? cts)
  {
    cts?.Cancel();
    cts?.Dispose();
  }

  private void ClearPendingCredentials()
  {
    _pendingEmail = null;
    _pendingPassword = null;
  }

  private async Task<InteractiveLoginResult> ExecuteInteractiveLogin(LoginRequestDto request, CancellationToken cancellationToken)
  {
    try
    {
      var client = _httpClientFactory.CreateClient(ControlrApiClientNames.UnauthenticatedClient);
      using var response = await client.PostAsJsonAsync(
        new Uri(BaseUrl, InteractiveLoginEndpoint),
        request,
        cancellationToken);

      if (response.StatusCode == HttpStatusCode.Unauthorized)
      {
        return new InteractiveLoginResult(InteractiveLoginStatus.Failed);
      }

      await response.EnsureSuccessStatusCodeWithDetails();

      var payload = await response.Content.ReadFromJsonAsync<InteractiveLoginResponseDto>(cancellationToken) ??
        throw new HttpRequestException("The interactive login response was empty.");

      if (payload.RequiresTwoFactor)
      {
        return new InteractiveLoginResult(InteractiveLoginStatus.RequiresTwoFactor);
      }

      if (payload.Tokens is null)
      {
        throw new HttpRequestException("The interactive login response did not include tokens.");
      }

      _authState.SetBearerTokenResponse(payload.Tokens, _timeProvider);
      UpdateState(ControlrAuthSessionState.Authenticated);
      StartRefreshLoop();
      return new InteractiveLoginResult(InteractiveLoginStatus.Authenticated);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Interactive login failed.");
      return new InteractiveLoginResult(InteractiveLoginStatus.Failed, ex.Message);
    }
  }

  private Task HandleRefreshLoopFault(long generation, string message)
  {
    if (generation != Volatile.Read(ref _refreshLoopGeneration))
    {
      return Task.CompletedTask;
    }

    ResetSession(clearPersonalAccessToken: false);
    UpdateState(ControlrAuthSessionState.Expired, message);
    return Task.CompletedTask;
  }

  private async Task RefreshBearerTokenIfNeeded(bool forceRefresh, CancellationToken cancellationToken)
  {
    var refreshResult = await _bearerTokenRefresher.RefreshIfNeeded(
      forceRefresh,
      _refreshLeadTime,
      BaseUrl,
      cancellationToken);

    if (refreshResult == BearerTokenRefreshResult.Unauthorized)
    {
      throw new InvalidOperationException("The refresh token is no longer valid.");
    }

    if (refreshResult == BearerTokenRefreshResult.EndpointUnavailable)
    {
      throw new InvalidOperationException("The bearer token refresh endpoint is not available.");
    }

    if (refreshResult == BearerTokenRefreshResult.Refreshed &&
        State != ControlrAuthSessionState.Authenticated)
    {
      UpdateState(ControlrAuthSessionState.Authenticated);
    }
  }

  private void ResetSession(bool clearPersonalAccessToken)
  {
    StopRefreshLoop();
    ClearPendingCredentials();
    _authState.ClearBearerTokens();

    if (clearPersonalAccessToken)
    {
      _authState.ClearPersonalAccessToken();
    }
  }

  private async Task RunRefreshLoop(long generation, CancellationToken cancellationToken)
  {
    try
    {
      while (!cancellationToken.IsCancellationRequested)
      {
        if (generation != Volatile.Read(ref _refreshLoopGeneration))
        {
          return;
        }

        var expiresAt = _authState.GetSnapshot().BearerTokenExpiresAt;
        if (expiresAt is null)
        {
          return;
        }

        var delay = expiresAt.Value - _timeProvider.GetUtcNow() - _refreshLeadTime;
        if (delay < TimeSpan.Zero)
        {
          delay = TimeSpan.Zero;
        }

        await Task.Delay(delay, cancellationToken);
        await RefreshBearerTokenIfNeeded(forceRefresh: false, cancellationToken);
      }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Bearer token refresh failed in the background.");
      await HandleRefreshLoopFault(generation, "The session expired. Sign in again.");
    }
  }

  private void StartRefreshLoop()
  {
    if (!_authState.CanRefreshBearerToken)
    {
      StopRefreshLoop();
      return;
    }

    var cts = new CancellationTokenSource();
    var generation = Interlocked.Increment(ref _refreshLoopGeneration);
    var previousCts = Interlocked.Exchange(ref _refreshLoopCts, cts);
    CancelRefreshLoop(previousCts);
    _ = RunRefreshLoop(generation, cts.Token);
  }

  private void StopRefreshLoop()
  {
    Interlocked.Increment(ref _refreshLoopGeneration);
    var cts = Interlocked.Exchange(ref _refreshLoopCts, null);
    CancelRefreshLoop(cts);
  }

  private void UpdateState(ControlrAuthSessionState state, string? message = null)
  {
    _state = state;
    StateChanged?.Invoke(this, new ControlrAuthSessionStateChangedEventArgs(state, message));
  }
}