using System.Net;
using System.Net.Http.Json;
using ControlR.ApiClient.Auth;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
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
  /// Gets the current server base URL used for interactive sign-in and token refresh calls.
  /// </summary>
  Uri BaseUrl { get; }
  /// <summary>
  /// Gets the access-token expiration for the current bearer session, if one exists.
  /// </summary>
  DateTimeOffset? BearerTokenExpiresAt { get; }
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
  /// Completes the unauthenticated password-change flow for a user who must change their password before finishing sign-in.
  /// </summary>
  /// <param name="email">The email address for the account being updated.</param>
  /// <param name="currentPassword">The user's current password.</param>
  /// <param name="newPassword">The new password to set for the account.</param>
  /// <param name="twoFactorCode">An optional two-factor code required by the server for this flow.</param>
  /// <param name="cancellationToken">Cancels the password-change request.</param>
  /// <returns>
  /// A result indicating whether the password was changed successfully.
  /// </returns>
  Task<ApiResult> ChangePasswordWithCredentials(string email, string currentPassword, string newPassword, string? twoFactorCode, CancellationToken cancellationToken = default);
  /// <summary>
  /// Gets a snapshot of the current authentication state, including bearer and refresh tokens.
  /// Use this for persistence (e.g. caching tokens in a secure keychain for automatic re-auth).
  /// </summary>
  /// <returns>The current auth snapshot.</returns>
  AuthSnapshot GetAuthSnapshot();
  /// <summary>
  /// Gets a usable bearer access token, refreshing it first when needed.
  /// </summary>
  /// <param name="cancellationToken">Cancels the token retrieval operation.</param>
  /// <returns>
  /// The current bearer access token, or <see langword="null"/> when the session is using a personal access token.
  /// </returns>
  Task<string?> GetBearerToken(CancellationToken cancellationToken = default);
  /// <summary>
  /// Restores a previously captured <see cref="AuthSnapshot"/>.
  /// If the snapshot contains a personal access token it is restored as a PAT session (state: <see cref="ControlrAuthSessionState.PatConfigured"/>).
  /// Otherwise bearer tokens are restored and the background token-refresh loop is started (state: <see cref="ControlrAuthSessionState.Authenticated"/>).
  /// </summary>
  /// <param name="snapshot">The previously captured auth snapshot.</param>
  Task RestoreAuthSnapshot(AuthSnapshot snapshot);
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
  /// Starts an interactive sign-in using an email and password, with optional two-factor credentials.
  /// </summary>
  /// <param name="request">The sign-in request details.</param>
  /// <param name="cancellationToken">Cancels the sign-in operation.</param>
  /// <returns>The result of the interactive login attempt.</returns>
  Task<InteractiveLoginResult> SignIn(InteractiveSignInRequest request, CancellationToken cancellationToken = default);
  /// <summary>
  /// Signs out of the interactive bearer session and clears stored authentication state.
  /// </summary>
  Task SignOut();
}

public sealed class ControlrAuthSession(
  IHttpClientFactory httpClientFactory,
  ControlrApiClientAuthState authState,
  IBearerTokenRefresher bearerTokenRefresher,
  ILogger<ControlrAuthSession> logger,
  IOptionsMonitor<ControlrApiClientOptions> optionsMonitor,
  TimeProvider timeProvider) : IControlrAuthSession
{
  private const string InteractiveLoginEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/interactive-login";

  /// <summary>
  /// Consecutive background refresh failures tolerated before the session is expired.
  /// </summary>
  private const int MaxConsecutiveRefreshFailures = 10;

  private static readonly TimeSpan _maxRefreshRetryBackoff = TimeSpan.FromSeconds(60);
  private static readonly TimeSpan _minRefreshRetryBackoff = TimeSpan.FromMilliseconds(250);

  private readonly ControlrApiClientAuthState _authState = authState;
  private readonly IBearerTokenRefresher _bearerTokenRefresher = bearerTokenRefresher;
  private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
  private readonly ILogger<ControlrAuthSession> _logger = logger;
  private readonly IOptionsMonitor<ControlrApiClientOptions> _optionsMonitor = optionsMonitor;
  private readonly TimeProvider _timeProvider = timeProvider;

  private Uri _baseUrl = optionsMonitor.CurrentValue.BaseUrl;
  private int _consecutiveRefreshFailures;
  private int _isDisposed;
  private CancellationTokenSource? _refreshLoopCts;
  private long _refreshLoopGeneration;
  private ControlrAuthSessionState _state = ControlrAuthSessionState.SignedOut;

  public event EventHandler<ControlrAuthSessionStateChangedEventArgs>? StateChanged;

  public Uri BaseUrl => Volatile.Read(ref _baseUrl);
  public DateTimeOffset? BearerTokenExpiresAt => _authState.BearerTokenExpiresAt;
  public bool IsAuthenticated => State == ControlrAuthSessionState.Authenticated;
  public string? PersonalAccessToken => _authState.PersonalAccessToken;
  public bool RequiresTwoFactor => State == ControlrAuthSessionState.AwaitingTwoFactor;
  public ControlrAuthSessionState State => _state;

  /// <summary>
  /// Counts this session's calls so that a tracked target is not released while a sign-in or password
  /// change is still on the wire. The factory assigns the target's tracker here; the single-client
  /// registration keeps the default instance, where nothing ever requests teardown.
  /// </summary>
  internal InFlightTracker Requests { get; set; } = new();

  public async Task<ApiResult> ChangePasswordWithCredentials(string email, string currentPassword, string newPassword, string? twoFactorCode, CancellationToken cancellationToken = default)
  {
    using var tracked = BeginSessionRequest();

    try
    {
      var client = _httpClientFactory.CreateClient(ControlrApiClientNames.UnauthenticatedClient);
      using var response = await client.PostAsJsonAsync(
        new Uri(BaseUrl, $"{HttpConstants.Internal.AuthEndpoint}/change-password-with-credentials"),
        new CredentialPasswordChangeRequestDto(email, currentPassword, newPassword, twoFactorCode),
        cancellationToken);

      if (response.StatusCode is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError)
      {
        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return ApiResult.Fail(error, response.StatusCode);
      }

      await response.EnsureSuccessStatusCodeWithDetails();
      return ApiResult.Ok();
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      _logger.LogWarning("Password change canceled by the user.");
      return ApiResult.Fail("The operation was canceled.", HttpStatusCode.RequestTimeout);
    }
    catch (TimeoutException ex)
    {
      _logger.LogWarning(ex, "Password change request timed out.");
      return ApiResult.Fail("The request timed out.", HttpStatusCode.RequestTimeout);
    }
    catch (HttpRequestException ex)
    {
      _logger.LogWarning(ex, "Password change request failed.");
      var statusCode = ex.StatusCode ?? HttpStatusCode.InternalServerError;
      return ApiResult.Fail(ex.Message, statusCode);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Password change failed.");
      return ApiResult.Fail("Password change failed. Please try again.", HttpStatusCode.InternalServerError);
    }
  }

  public void Dispose()
  {
    Volatile.Write(ref _isDisposed, 1);
    StopRefreshLoop();
  }

  public AuthSnapshot GetAuthSnapshot()
  {
    return _authState.GetSnapshot();
  }

  /// <summary>
  /// Gets the current bearer token, refreshing it first when it is near expiration.
  /// </summary>
  /// <param name="cancellationToken">Cancels the refresh request.</param>
  /// <returns>The bearer token, or <see langword="null"/> when this session authenticates with a personal access token.</returns>
  /// <exception cref="ObjectDisposedException">
  /// The session's target was removed, so the token cannot be renewed. A token that is already dead
  /// is not handed out as if it were usable.
  /// </exception>
  public async Task<string?> GetBearerToken(CancellationToken cancellationToken = default)
  {
    if (!string.IsNullOrWhiteSpace(_authState.PersonalAccessToken))
    {
      return null;
    }

    await RefreshBearerTokenIfNeeded(forceRefresh: false, cancellationToken);
    return _authState.BearerToken;
  }

  /// <summary>
  /// Restores a previously saved auth snapshot into this session.
  /// </summary>
  /// <param name="snapshot">The snapshot to apply.</param>
  /// <exception cref="ArgumentException">The snapshot contains neither a personal access token nor a complete set of bearer tokens.</exception>
  public Task RestoreAuthSnapshot(AuthSnapshot snapshot)
  {
    if (!string.IsNullOrWhiteSpace(snapshot.PersonalAccessToken))
    {
      SetPersonalAccessToken(snapshot.PersonalAccessToken);
      return Task.CompletedTask;
    }

    if (string.IsNullOrWhiteSpace(snapshot.BearerToken) ||
        string.IsNullOrWhiteSpace(snapshot.RefreshToken) ||
        snapshot.BearerTokenExpiresAt is null)
    {
      throw new ArgumentException(
        "Snapshot must contain a personal access token or valid bearer and refresh tokens with an expiration.",
        nameof(snapshot));
    }

    ResetSession(clearPersonalAccessToken: true);
    _authState.SetBearerTokens(snapshot.BearerToken, snapshot.RefreshToken, snapshot.BearerTokenExpiresAt);
    UpdateState(ControlrAuthSessionState.Authenticated);
    StartRefreshLoop();
    return Task.CompletedTask;
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
      UpdateState(ControlrAuthSessionState.PatConfigured);
    }
    else
    {
      UpdateState(ControlrAuthSessionState.SignedOut);
    }
  }

  public async Task<InteractiveLoginResult> SignIn(InteractiveSignInRequest request, CancellationToken cancellationToken = default)
  {
    var result = await ExecuteInteractiveLogin(
      new LoginRequestDto(
        request.Email,
        request.Password,
        string.IsNullOrWhiteSpace(request.TwoFactorCode) ? null : request.TwoFactorCode,
        string.IsNullOrWhiteSpace(request.RecoveryCode) ? null : request.RecoveryCode),
      cancellationToken);

    if (result.Status == InteractiveLoginStatus.RequiresPasswordChange)
    {
      UpdateState(ControlrAuthSessionState.AwaitingPasswordChange);
      return result with { Message = "A password change is required. Enter your current and a new password to continue." };
    }

    if (result.Status == InteractiveLoginStatus.RequiresTwoFactor)
    {
      UpdateState(ControlrAuthSessionState.AwaitingTwoFactor);
      return result with { Message = "Two-factor authentication is enabled. Enter your authenticator code to continue." };
    }

    if (result.Status == InteractiveLoginStatus.LockedOut)
    {
      return result;
    }

    if (result.Status == InteractiveLoginStatus.Authenticated)
    {
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

  private static void CancelRefreshLoop(CancellationTokenSource? cts)
  {
    cts?.Cancel();
    cts?.Dispose();
  }

  // Session calls go to the target's unauthenticated client rather than through ControlrApi, so
  // nothing else counts them. A removal mid-call would otherwise be reported as bad credentials.
  private InFlightTracker.Lease BeginSessionRequest() => Requests.AcquireOrThrow(nameof(ControlrAuthSession));

  private async Task<InteractiveLoginResult> ExecuteInteractiveLogin(LoginRequestDto request, CancellationToken cancellationToken)
  {
    using var tracked = BeginSessionRequest();

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

  private void ExpireSession(string message)
  {
    ResetSession(clearPersonalAccessToken: false);
    UpdateState(ControlrAuthSessionState.Expired, message);
  }

  private Task HandleRefreshLoopFault(long generation, string message)
  {
    if (generation != Volatile.Read(ref _refreshLoopGeneration))
    {
      return Task.CompletedTask;
    }

    ExpireSession(message);
    return Task.CompletedTask;
  }

  private async Task RefreshBearerTokenIfNeeded(
    bool forceRefresh,
    CancellationToken cancellationToken,
    long? loopGeneration = null)
  {
    var refreshResult = await _bearerTokenRefresher.RefreshIfNeeded(
      forceRefresh,
      _optionsMonitor.CurrentValue.BearerRefreshLeadTime,
      BaseUrl,
      cancellationToken);

    if (refreshResult == BearerTokenRefreshResult.Unauthorized)
    {
      // A superseded background loop must not expire a session that the current loop (or an
      // interactive foreground call) now owns. The response was computed with the previous
      // session's refresh token, so its rejection says nothing about the current one.
      if (loopGeneration is null || loopGeneration == Volatile.Read(ref _refreshLoopGeneration))
      {
        ExpireSession("The session expired. Sign in again.");
      }

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

        try
        {
          await RefreshBearerTokenIfNeeded(forceRefresh: false, cancellationToken, loopGeneration: generation);
          _consecutiveRefreshFailures = 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
          // A cancelled or superseded loop must not touch shared failure state, retry, or
          // expire anything. The loop owning the current generation owns the counter.
          if (generation != Volatile.Read(ref _refreshLoopGeneration))
          {
            return;
          }

          if (ex is ObjectDisposedException)
          {
            // The target this session belongs to was removed, so there is no transport left to
            // renew it with. Counting this against the transient-failure budget would spend the
            // backoff schedule reporting a fault on a target that is simply gone.
            return;
          }

          if (State == ControlrAuthSessionState.Expired)
          {
            // The server rejected the refresh token itself. RefreshBearerTokenIfNeeded has
            // already transitioned the session to Expired; nothing left to retry.
            return;
          }

          // A transient failure (network hiccup, timeout, 5xx, 404) must not discard a refresh
          // token that is often valid for days. Retry with a clamped backoff, but cap the
          // attempt count so a permanently broken server cannot produce an endless retry loop.
          _consecutiveRefreshFailures++;
          if (_consecutiveRefreshFailures >= MaxConsecutiveRefreshFailures)
          {
            _logger.LogWarning(
              ex,
              "Bearer token refresh failed {FailureCount} consecutive times. Expiring the session.",
              _consecutiveRefreshFailures);
            await HandleRefreshLoopFault(
              generation,
              "Unable to renew the session. Sign in again.");
            return;
          }

          _logger.LogWarning(ex, "Bearer token refresh failed in the background. Retrying.");
          var backoff = _optionsMonitor.CurrentValue.BearerRefreshLeadTime * 2;
          if (backoff < _minRefreshRetryBackoff)
          {
            backoff = _minRefreshRetryBackoff;
          }
          else if (backoff > _maxRefreshRetryBackoff)
          {
            backoff = _maxRefreshRetryBackoff;
          }

          await Task.Delay(backoff, cancellationToken);
        }
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
    if (Volatile.Read(ref _isDisposed) == 1)
    {
      // A sign-in that was still on the wire when the target was removed lands here after the
      // factory already disposed this session. A loop started now has nobody left to stop it.
      return;
    }

    if (!_authState.CanRefreshBearerToken)
    {
      StopRefreshLoop();
      return;
    }

    // Fresh loop, fresh failure budget. Otherwise a previous session's failures would expire
    // a newly authenticated session after fewer failures than the configured cap.
    _consecutiveRefreshFailures = 0;

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