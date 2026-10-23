using System.Net;
using System.Net.Http.Json;
using ControlR.ApiClient.Auth;
using ControlR.ApiClient.Internal;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using Microsoft.Extensions.Logging;

namespace ControlR.ApiClient;

/// <summary>
/// Manages interactive bearer-session authentication state for ControlR API clients.
/// </summary>
public interface IControlrAuthSession : IDisposable
{
  /// <summary>
  /// Raised whenever the session state changes, including the transition to
  /// <see cref="ControlrAuthSessionState.Disposed"/> when the session is disposed. Disposing a session
  /// that was still <see cref="ControlrAuthSessionState.SignedOut"/> raises nothing, because that state
  /// already tells an observer the session cannot authenticate.
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
  /// Gets the configured service account credential, if one is being used instead of interactive
  /// bearer auth. Sent as the <c>x-api-key</c> header.
  /// </summary>
  string? ServiceAccountApiKey { get; }
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
  /// <returns>A result indicating whether the password was changed successfully.</returns>
  Task<ApiResult> ChangePasswordWithCredentials(string email, string currentPassword, string newPassword, string? twoFactorCode, CancellationToken cancellationToken = default);
  /// <summary>
  /// Gets a snapshot of the current authentication state, including bearer and refresh tokens.
  /// Use this for persistence (e.g. caching tokens in a secure keychain for automatic re-auth).
  /// </summary>
  /// <returns>The current auth snapshot.</returns>
  AuthSnapshot GetAuthSnapshot();
  /// <summary>
  /// Gets a usable bearer access token, renewing it first when it is close to expiring.
  /// </summary>
  /// <param name="cancellationToken">Cancels the wait for a renewal. The renewal itself still completes.</param>
  /// <returns>
  /// The current bearer access token, or <see langword="null"/> when the session authenticates with a
  /// configured credential (a personal access token or a service account key) instead of a bearer token.
  /// </returns>
  Task<string?> GetBearerToken(CancellationToken cancellationToken = default);
  /// <summary>
  /// Restores a previously captured <see cref="AuthSnapshot"/>, re-establishing the session kind it held.
  /// </summary>
  /// <param name="snapshot">The previously captured auth snapshot.</param>
  /// <exception cref="ArgumentException">The snapshot holds no usable credential.</exception>
  /// <exception cref="ObjectDisposedException">This session was disposed.</exception>
  Task RestoreAuthSnapshot(AuthSnapshot snapshot);
  /// <summary>
  /// Updates the server base URL used by the session.
  /// </summary>
  /// <param name="baseUrl">The ControlR server base URL.</param>
  void SetBaseUrl(Uri baseUrl);
  /// <summary>
  /// Configures a personal access token for the session and clears any other credential.
  /// </summary>
  /// <param name="personalAccessToken">The personal access token to use, or <see langword="null"/> to clear it.</param>
  void SetPersonalAccessToken(string? personalAccessToken);
  /// <summary>
  /// Configures a service account credential for the session, sent as the <c>x-api-key</c> header, and
  /// clears any other credential.
  /// </summary>
  /// <param name="serviceAccountApiKey">
  /// The composite credential (<c>{credentialIdHex}:{secret}</c>) to use, or <see langword="null"/> to clear it.
  /// </param>
  void SetServiceAccountApiKey(string? serviceAccountApiKey);
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

/// <summary>
/// The sole owner of a client's bearer token. Renewal happens on demand, before a request whose token
/// is close to expiring and after a request whose token was rejected. There is no background loop.
/// </summary>
public sealed class ControlrAuthSession : IControlrAuthSession
{
  private const string ChangePasswordEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/change-password-with-credentials";
  private const string InteractiveLoginEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/interactive-login";
  private const string RefreshEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/refresh";

  private readonly ControlrApiClientAuthState _authState;
  private readonly CancellationTokenSource _disposeCts = new();
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly ILogger<ControlrAuthSession> _logger;
  private readonly TimeSpan _refreshLeadTime;
  private readonly Lock _renewalLock = new();
  private readonly Lock _stateLock = new();
  private readonly TimeProvider _timeProvider;

  private Uri _baseUrl;
  private Task<BearerCredential?>? _renewal;
  private BearerCredential? _renewalTarget;
  private volatile ControlrAuthSessionState _state;

  internal ControlrAuthSession(
    IHttpClientFactory httpClientFactory,
    ControlrApiClientAuthState authState,
    ControlrApiClientOptions options,
    TimeProvider timeProvider,
    ILogger<ControlrAuthSession> logger)
  {
    _httpClientFactory = httpClientFactory;
    _authState = authState;
    _baseUrl = options.BaseUrl;
    _refreshLeadTime = options.BearerRefreshLeadTime;
    _timeProvider = timeProvider;
    _logger = logger;
    _state = GetConfiguredState(authState.GetCredentials());
  }

  public event EventHandler<ControlrAuthSessionStateChangedEventArgs>? StateChanged;

  public Uri BaseUrl => Volatile.Read(ref _baseUrl);
  public DateTimeOffset? BearerTokenExpiresAt => _authState.BearerTokenExpiresAt;
  public bool IsAuthenticated => State == ControlrAuthSessionState.Authenticated;
  public string? PersonalAccessToken => _authState.PersonalAccessToken;
  public bool RequiresTwoFactor => State == ControlrAuthSessionState.AwaitingTwoFactor;
  public string? ServiceAccountApiKey => _authState.ServiceAccountApiKey;
  public ControlrAuthSessionState State => _state;

  /// <summary>
  /// Counts this session's calls so a tracked target is not released while one is on the wire.
  /// The factory assigns the target's tracker.
  /// </summary>
  internal InFlightTracker Requests { get; set; } = new();

  public async Task<ApiResult> ChangePasswordWithCredentials(string email, string currentPassword, string newPassword, string? twoFactorCode, CancellationToken cancellationToken = default)
  {
    using var lease = Requests.Acquire();
    if (!lease.Acquired)
    {
      return ApiResult.Fail(ControlrApi.DisposedTargetReason, httpRequestError: HttpRequestError.Unknown);
    }

    try
    {
      var request = new CredentialPasswordChangeRequestDto(email, currentPassword, newPassword, twoFactorCode);
      using var response = await CreateClient().PostAsJsonAsync(new Uri(BaseUrl, ChangePasswordEndpoint), request, cancellationToken);

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
      return ApiResult.Fail("The operation was canceled.", HttpStatusCode.RequestTimeout);
    }
    catch (HttpRequestException ex)
    {
      _logger.LogWarning(ex, "Password change request failed.");
      return ApiResult.Fail(ex.Message, ex.StatusCode ?? HttpStatusCode.InternalServerError);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Password change failed.");
      return ApiResult.Fail("Password change failed. Please try again.", HttpStatusCode.InternalServerError);
    }
  }

  /// <summary>
  /// Moves the session to the terminal <see cref="ControlrAuthSessionState.Disposed"/> state and
  /// cancels a renewal that is still on the wire. This is not a sign-out.
  /// </summary>
  public void Dispose()
  {
    ControlrAuthSessionState previous;
    using (_stateLock.EnterScope())
    {
      previous = _state;
      if (previous == ControlrAuthSessionState.Disposed)
      {
        return;
      }

      _state = ControlrAuthSessionState.Disposed;
    }

    _disposeCts.Cancel();

    if (previous == ControlrAuthSessionState.SignedOut)
    {
      return;
    }

    StateChanged?.Invoke(
      this,
      new ControlrAuthSessionStateChangedEventArgs(
        ControlrAuthSessionState.Disposed,
        "The session was disposed. Obtain a new session to authenticate again."));
  }

  public AuthSnapshot GetAuthSnapshot() => _authState.GetSnapshot();

  public async Task<string?> GetBearerToken(CancellationToken cancellationToken = default)
  {
    if (!string.IsNullOrWhiteSpace(_authState.PersonalAccessToken))
    {
      return null;
    }

    var bearer = await GetFreshBearer(cancellationToken);
    return bearer?.AccessToken;
  }

  public Task RestoreAuthSnapshot(AuthSnapshot snapshot)
  {
    ArgumentNullException.ThrowIfNull(snapshot);
    ObjectDisposedException.ThrowIf(_state == ControlrAuthSessionState.Disposed, this);

    if (!string.IsNullOrWhiteSpace(snapshot.PersonalAccessToken))
    {
      SetPersonalAccessToken(snapshot.PersonalAccessToken);
      return Task.CompletedTask;
    }

    if (!string.IsNullOrWhiteSpace(snapshot.ServiceAccountApiKey))
    {
      SetServiceAccountApiKey(snapshot.ServiceAccountApiKey);
      return Task.CompletedTask;
    }

    if (string.IsNullOrWhiteSpace(snapshot.BearerToken) ||
        string.IsNullOrWhiteSpace(snapshot.RefreshToken) ||
        snapshot.BearerTokenExpiresAt is not { } expiresAt)
    {
      throw new ArgumentException(
        "Snapshot must contain a personal access token, a service account credential, or valid bearer and refresh tokens with an expiration.",
        nameof(snapshot));
    }

    var bearer = new BearerCredential(snapshot.BearerToken, snapshot.RefreshToken, expiresAt, snapshot.BearerTokenLifetime);
    _authState.Replace(new ApiCredentials(null, null, bearer));
    UpdateState(ControlrAuthSessionState.Authenticated);
    return Task.CompletedTask;
  }

  public void SetBaseUrl(Uri baseUrl)
  {
    ArgumentNullException.ThrowIfNull(baseUrl);
    Volatile.Write(ref _baseUrl, baseUrl);
  }

  public void SetPersonalAccessToken(string? personalAccessToken)
  {
    var credentials = new ApiCredentials(NullIfBlank(personalAccessToken), null, null);
    _authState.Replace(credentials);
    UpdateState(GetConfiguredState(credentials));
  }

  public void SetServiceAccountApiKey(string? serviceAccountApiKey)
  {
    var credentials = new ApiCredentials(null, NullIfBlank(serviceAccountApiKey), null);
    _authState.Replace(credentials);
    UpdateState(GetConfiguredState(credentials));
  }

  public async Task<InteractiveLoginResult> SignIn(InteractiveSignInRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var loginRequest = new LoginRequestDto(
      request.Email,
      request.Password,
      NullIfBlank(request.TwoFactorCode),
      NullIfBlank(request.RecoveryCode));

    using var lease = Requests.Acquire();
    if (!lease.Acquired)
    {
      return new InteractiveLoginResult(InteractiveLoginStatus.Failed, ControlrApi.DisposedTargetReason);
    }

    InteractiveLoginResponseDto? payload;
    try
    {
      using var response = await CreateClient().PostAsJsonAsync(new Uri(BaseUrl, InteractiveLoginEndpoint), loginRequest, cancellationToken);

      if (response.StatusCode == HttpStatusCode.Unauthorized)
      {
        return new InteractiveLoginResult(InteractiveLoginStatus.Failed, "Email or password was not accepted.");
      }

      if (response.StatusCode == HttpStatusCode.NotFound)
      {
        return new InteractiveLoginResult(InteractiveLoginStatus.Failed, "Interactive login is not available on this server.");
      }

      await response.EnsureSuccessStatusCodeWithDetails();
      payload = await response.Content.ReadFromJsonAsync<InteractiveLoginResponseDto>(cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Interactive login failed.");
      return new InteractiveLoginResult(InteractiveLoginStatus.Failed, "Interactive login failed.");
    }

    if (payload is null)
    {
      return new InteractiveLoginResult(InteractiveLoginStatus.Failed, "Interactive login failed.");
    }

    if (payload.IsLockedOut)
    {
      return new InteractiveLoginResult(InteractiveLoginStatus.LockedOut, "This account has been locked out. Please try again later.");
    }

    if (payload.RequiresTwoFactor)
    {
      UpdateState(ControlrAuthSessionState.AwaitingTwoFactor);
      return new InteractiveLoginResult(
        InteractiveLoginStatus.RequiresTwoFactor,
        "Two-factor authentication is enabled. Enter your authenticator code to continue.");
    }

    if (payload.RequiresPasswordChange)
    {
      UpdateState(ControlrAuthSessionState.AwaitingPasswordChange);
      return new InteractiveLoginResult(
        InteractiveLoginStatus.RequiresPasswordChange,
        "A password change is required. Enter your current and a new password to continue.");
    }

    if (payload.Tokens is null)
    {
      // A success response that carries neither a password-change flag nor tokens is a broken
      // response, not a flow the caller can complete. Reporting a failed sign-in keeps the session
      // usable; a password-change state would wait on a form that never arrives.
      _logger.LogWarning("Interactive login succeeded without returning tokens.");
      return new InteractiveLoginResult(InteractiveLoginStatus.Failed, "Interactive login failed.");
    }

    // A leftover personal access token would otherwise keep winning header precedence, so requests
    // would still authenticate as the old principal after sign-in.
    _authState.Replace(new ApiCredentials(null, null, CreateBearer(payload.Tokens)));
    UpdateState(ControlrAuthSessionState.Authenticated);
    return new InteractiveLoginResult(InteractiveLoginStatus.Authenticated, "Connected.");
  }

  public Task SignOut()
  {
    _authState.Replace(new ApiCredentials(null, null, null));
    UpdateState(ControlrAuthSessionState.SignedOut);
    return Task.CompletedTask;
  }

  /// <summary>
  /// Returns the current bearer credential, renewing it first when it is inside its refresh window.
  /// </summary>
  internal async Task<BearerCredential?> GetFreshBearer(CancellationToken cancellationToken)
  {
    var bearer = _authState.Bearer;
    if (bearer is null || !IsInRefreshWindow(bearer))
    {
      return bearer;
    }

    return await RenewIfCurrent(bearer, cancellationToken);
  }

  /// <summary>
  /// Renews <paramref name="sent"/> unless it was already replaced, and returns the bearer credential
  /// that is current afterward.
  /// </summary>
  /// <remarks>
  /// Every caller holding the same credential shares one refresh request. The refresh runs on the
  /// session's own token, so a caller that cancels stops waiting without cancelling it for the rest.
  /// </remarks>
  internal Task<BearerCredential?> RenewIfCurrent(BearerCredential sent, CancellationToken cancellationToken)
  {
    Task<BearerCredential?> renewal;
    using (_renewalLock.EnterScope())
    {
      var current = _authState.Bearer;
      if (!ReferenceEquals(current, sent))
      {
        return Task.FromResult(current);
      }

      if (_renewal is null || _renewal.IsCompleted || !ReferenceEquals(_renewalTarget, sent))
      {
        _renewalTarget = sent;
        // Task.Run keeps the refresh and the StateChanged it may raise out from under this lock.
        _renewal = Task.Run(() => Renew(sent));
      }

      renewal = _renewal;
    }

    return renewal.WaitAsync(cancellationToken);
  }

  private static ControlrAuthSessionState GetConfiguredState(ApiCredentials credentials)
  {
    if (!string.IsNullOrWhiteSpace(credentials.PersonalAccessToken))
    {
      return ControlrAuthSessionState.PatConfigured;
    }

    if (!string.IsNullOrWhiteSpace(credentials.ServiceAccountApiKey))
    {
      return ControlrAuthSessionState.ServiceAccountConfigured;
    }

    return ControlrAuthSessionState.SignedOut;
  }

  private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

  private BearerCredential CreateBearer(AccessTokenResponseDto tokens)
  {
    var lifetime = TimeSpan.FromSeconds(Math.Max(tokens.ExpiresInSeconds, 1));
    return new BearerCredential(tokens.AccessToken, tokens.RefreshToken, _timeProvider.GetUtcNow() + lifetime, lifetime);
  }

  private HttpClient CreateClient() => _httpClientFactory.CreateClient(ControlrApiClientNames.UnauthenticatedClient);

  /// <summary>
  /// The window is capped at half the granted lifetime. A server lifetime at or below the lead time
  /// would otherwise leave every fresh token already due for renewal.
  /// </summary>
  private bool IsInRefreshWindow(BearerCredential bearer)
  {
    var window = _refreshLeadTime;
    if (bearer.Lifetime is { } lifetime && lifetime / 2 < window)
    {
      window = lifetime / 2;
    }

    return _timeProvider.GetUtcNow() >= bearer.ExpiresAt - window;
  }

  private async Task<BearerCredential?> Renew(BearerCredential sent)
  {
    using var lease = Requests.Acquire();
    if (!lease.Acquired)
    {
      return _authState.Bearer;
    }

    try
    {
      using var response = await CreateClient().PostAsJsonAsync(
        new Uri(BaseUrl, RefreshEndpoint),
        new RefreshTokenRequestDto(sent.RefreshToken),
        _disposeCts.Token);

      if (response.StatusCode == HttpStatusCode.Unauthorized)
      {
        if (_authState.TryReplaceBearer(sent, null))
        {
          UpdateState(ControlrAuthSessionState.Expired, "The session expired. Sign in again.");
        }

        return _authState.Bearer;
      }

      await response.EnsureSuccessStatusCodeWithDetails();

      var tokens = await response.Content.ReadFromJsonAsync<AccessTokenResponseDto>(_disposeCts.Token) ??
        throw new HttpRequestException("The refresh response was empty.");

      _authState.TryReplaceBearer(sent, CreateBearer(tokens));
    }
    catch (Exception ex)
    {
      // A transient failure keeps the current token. The next request inside the window tries again.
      _logger.LogWarning(ex, "Bearer token refresh failed.");
    }

    return _authState.Bearer;
  }

  private void UpdateState(ControlrAuthSessionState state, string? message = null)
  {
    using (_stateLock.EnterScope())
    {
      // Terminal. A sign-in that finishes after disposal must not report a usable session.
      if (_state == ControlrAuthSessionState.Disposed)
      {
        return;
      }

      _state = state;
    }

    StateChanged?.Invoke(this, new ControlrAuthSessionStateChangedEventArgs(state, message));
  }
}
