using System.Net;
using System.Net.Http.Json;
using ControlR.ApiClient.Auth;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.ApiClient;

/// <summary>
/// Manages interactive bearer-session authentication state for ControlR API clients.
/// </summary>
public interface IControlrAuthSession : IDisposable
{
  /// <summary>
  /// Raised whenever the session state changes.
  /// </summary>
  event EventHandler<ControlrAuthSessionStateChangedEventArgs>? StateChanged;

  /// <summary>
  /// Gets the access-token expiration for the current bearer session, if one exists.
  /// </summary>
  DateTimeOffset? AccessTokenExpiresAt { get; }

  /// <summary>
  /// Gets the current server base URL used for interactive sign-in and token refresh calls.
  /// </summary>
  Uri BaseUrl { get; }

  /// <summary>
  /// Gets a value indicating whether the session currently has an authenticated bearer token.
  /// </summary>
  bool IsAuthenticated { get; }

  /// <summary>
  /// Gets the configured personal access token, if one is being used instead of interactive bearer auth.
  /// </summary>
  string? PersonalAccessToken { get; }

  /// <summary>
  /// Gets a value indicating whether the current sign-in flow is waiting for a two-factor code.
  /// </summary>
  bool RequiresTwoFactor { get; }

  /// <summary>
  /// Gets the current session state.
  /// </summary>
  ControlrAuthSessionState State { get; }

  /// <summary>
  /// Gets a usable bearer access token, refreshing it first when needed.
  /// </summary>
  /// <param name="cancellationToken">Cancels the token retrieval operation.</param>
  /// <returns>
  /// The current bearer access token, or <see langword="null"/> when the session is using a personal access token.
  /// </returns>
  Task<string?> GetAccessToken(CancellationToken cancellationToken = default);

  /// <summary>
  /// Updates the server base URL used by the session.
  /// </summary>
  /// <param name="baseUrl">The ControlR server base URL.</param>
  void SetBaseUrl(Uri baseUrl);

  /// <summary>
  /// Configures a personal access token for the session and clears any interactive bearer state.
  /// </summary>
  /// <param name="personalAccessToken">The personal access token to use, or <see langword="null"/> to clear it.</param>
  void SetPersonalAccessToken(string? personalAccessToken);

  /// <summary>
  /// Starts an interactive sign-in using an email and password.
  /// </summary>
  /// <param name="email">The user email.</param>
  /// <param name="password">The user password.</param>
  /// <param name="cancellationToken">Cancels the sign-in operation.</param>
  /// <returns>The result of the interactive login attempt.</returns>
  Task<InteractiveLoginResult> SignIn(string email, string password, CancellationToken cancellationToken = default);

  /// <summary>
  /// Signs out of the interactive bearer session and clears stored authentication state.
  /// </summary>
  Task SignOut();

  /// <summary>
  /// Continues a pending two-factor sign-in using a recovery code.
  /// </summary>
  /// <param name="recoveryCode">The recovery code supplied by the user.</param>
  /// <param name="cancellationToken">Cancels the sign-in operation.</param>
  /// <returns>The result of the interactive login attempt.</returns>
  Task<InteractiveLoginResult> SubmitRecoveryCode(string recoveryCode, CancellationToken cancellationToken = default);

  /// <summary>
  /// Continues a pending two-factor sign-in using an authenticator code.
  /// </summary>
  /// <param name="twoFactorCode">The authenticator code supplied by the user.</param>
  /// <param name="cancellationToken">Cancels the sign-in operation.</param>
  /// <returns>The result of the interactive login attempt.</returns>
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
  private static readonly TimeSpan _pendingCredentialLifetime = TimeSpan.FromMinutes(5);

  private readonly ControlrApiClientAuthState _authState = authState;
  private readonly IBearerTokenRefresher _bearerTokenRefresher = bearerTokenRefresher;
  private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
  private readonly ILogger<ControlrAuthSession> _logger = logger;
  private readonly IOptionsMonitor<ControlrApiClientOptions> _optionsMonitor = optionsMonitor;
  private readonly object _pendingCredentialsLock = new();
  private readonly TimeProvider _timeProvider = timeProvider;

  private Uri _baseUrl = optionsMonitor.CurrentValue.BaseUrl;
  private (string Email, string Password, DateTimeOffset ExpiresAt)? _pendingCredentials;
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

    await RefreshBearerTokenIfNeeded(forceRefresh: false, cancellationToken);
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
      lock (_pendingCredentialsLock)
      {
        _pendingCredentials = (email, password, _timeProvider.GetUtcNow().Add(_pendingCredentialLifetime));
      }

      UpdateState(ControlrAuthSessionState.AwaitingTwoFactor);
      return result with { Message = "Two-factor authentication is enabled. Enter your authenticator code to continue." };
    }

    if (result.Status == InteractiveLoginStatus.LockedOut)
    {
      return result;
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
    return await SubmitPendingLogin(
      twoFactorCode: null,
      recoveryCode,
      "The recovery code was rejected.",
      cancellationToken);
  }

  public async Task<InteractiveLoginResult> SubmitTwoFactorCode(string twoFactorCode, CancellationToken cancellationToken = default)
  {
    return await SubmitPendingLogin(
      twoFactorCode,
      recoveryCode: null,
      "The two-factor code was rejected.",
      cancellationToken);
  }

  private static void CancelRefreshLoop(CancellationTokenSource? cts)
  {
    cts?.Cancel();
    cts?.Dispose();
  }

  private void ClearPendingCredentials()
  {
    lock (_pendingCredentialsLock)
    {
      _pendingCredentials = null;
    }
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

      if (payload.IsLockedOut)
      {
        return new InteractiveLoginResult(InteractiveLoginStatus.LockedOut, "This account has been locked out. Please try again later.");
      }

      if (payload.RequiresPasswordChange)
      {
        return new InteractiveLoginResult(InteractiveLoginStatus.RequiresPasswordChange, "A password change is required.");
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
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
      _logger.LogWarning(ex, "Interactive login endpoint is not available.");
      return new InteractiveLoginResult(
        InteractiveLoginStatus.Failed,
        "Interactive login is not available on this server.");
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Interactive login failed.");
      return new InteractiveLoginResult(InteractiveLoginStatus.Failed, "Interactive login failed.");
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
      _optionsMonitor.CurrentValue.BearerRefreshLeadTime,
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

        var delay = expiresAt.Value - _timeProvider.GetUtcNow() - _optionsMonitor.CurrentValue.BearerRefreshLeadTime;
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

  private async Task<InteractiveLoginResult> SubmitPendingLogin(
    string? twoFactorCode,
    string? recoveryCode,
    string rejectedMessage,
    CancellationToken cancellationToken)
  {
    LoginRequestDto? request;
    var expired = false;

    lock (_pendingCredentialsLock)
    {
      if (_pendingCredentials is not { } pendingCredentials)
      {
        request = null;
      }
      else if (pendingCredentials.ExpiresAt <= _timeProvider.GetUtcNow())
      {
        _pendingCredentials = null;
        request = null;
        expired = true;
      }
      else
      {
        request = new LoginRequestDto(pendingCredentials.Email, pendingCredentials.Password, twoFactorCode, recoveryCode);
      }
    }

    if (expired)
    {
      UpdateState(ControlrAuthSessionState.SignedOut, "The login attempt expired. Sign in again.");
      return new InteractiveLoginResult(InteractiveLoginStatus.Failed, "The login attempt expired. Sign in again.");
    }

    if (request is null)
    {
      return new InteractiveLoginResult(InteractiveLoginStatus.Failed, "A login attempt is not waiting for two-factor authentication.");
    }

    var result = await ExecuteInteractiveLogin(request, cancellationToken);
    if (result.Status == InteractiveLoginStatus.Authenticated)
    {
      ClearPendingCredentials();
      return result with { Message = "Connected." };
    }

    return result.Message is null
      ? result with { Message = rejectedMessage }
      : result;
  }

  private void UpdateState(ControlrAuthSessionState state, string? message = null)
  {
    _state = state;
    StateChanged?.Invoke(this, new ControlrAuthSessionStateChangedEventArgs(state, message));
  }
}