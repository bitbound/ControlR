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

  Task<string?> GetAccessToken();
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
  ILogger<ControlrAuthSession> logger,
  IOptionsMonitor<ControlrApiClientOptions> optionsMonitor,
  TimeProvider timeProvider) : IControlrAuthSession
{
  private const string InteractiveLoginEndpoint = $"{HttpConstants.AuthEndpoint}/interactive-login";
  private const string RefreshEndpoint = $"{HttpConstants.AuthEndpoint}/refresh";

  private static readonly TimeSpan _refreshLeadTime = TimeSpan.FromMinutes(1);

  private readonly ControlrApiClientAuthState _authState = authState;
  private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
  private readonly ILogger<ControlrAuthSession> _logger = logger;
  private readonly IOptionsMonitor<ControlrApiClientOptions> _optionsMonitor = optionsMonitor;
  private readonly TimeProvider _timeProvider = timeProvider;

  private string? _pendingEmail;
  private string? _pendingPassword;
  private CancellationTokenSource? _refreshLoopCts;
  private ControlrAuthSessionState _state = ControlrAuthSessionState.SignedOut;

  public event EventHandler<ControlrAuthSessionStateChangedEventArgs>? StateChanged;

  public DateTimeOffset? AccessTokenExpiresAt => _authState.BearerTokenExpiresAt;
  public Uri BaseUrl => _optionsMonitor.CurrentValue.BaseUrl;
  public bool IsAuthenticated => State == ControlrAuthSessionState.Authenticated;
  public string? PersonalAccessToken => _authState.PersonalAccessToken;
  public bool RequiresTwoFactor => State == ControlrAuthSessionState.AwaitingTwoFactor;
  public ControlrAuthSessionState State => _state;

  public void Dispose()
  {
    StopRefreshLoop();
  }

  public async Task<string?> GetAccessToken()
  {
    if (!string.IsNullOrWhiteSpace(_authState.PersonalAccessToken))
    {
      return null;
    }

    try
    {
      await RefreshBearerTokenIfNeeded(forceRefresh: false, CancellationToken.None);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to refresh bearer token before providing access token.");
    }

    return _authState.BearerToken;
  }

  public void SetBaseUrl(Uri baseUrl)
  {
    _optionsMonitor.CurrentValue.BaseUrl = baseUrl;
  }

  public void SetPersonalAccessToken(string? personalAccessToken)
  {
    ResetSession(clearPersonalAccessToken: true);

    if (!string.IsNullOrWhiteSpace(personalAccessToken))
    {
      _authState.PersonalAccessToken = personalAccessToken;
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
      using var response = await client.PostAsJsonAsync(InteractiveLoginEndpoint, request, cancellationToken);

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

  private async Task HandleRefreshLoopFault(string message)
  {
    ResetSession(clearPersonalAccessToken: false);
    UpdateState(ControlrAuthSessionState.Expired, message);
    await Task.CompletedTask;
  }

  private async Task RefreshBearerTokenIfNeeded(bool forceRefresh, CancellationToken cancellationToken)
  {
    var auth = _authState;
    if (!auth.CanRefreshBearerToken)
    {
      return;
    }

    if (!forceRefresh && !auth.ShouldRefreshBearerToken(_timeProvider, _refreshLeadTime))
    {
      return;
    }

    await auth.BearerRefreshLock.WaitAsync(cancellationToken);
    try
    {
      if (!auth.CanRefreshBearerToken)
      {
        return;
      }

      if (!forceRefresh && !auth.ShouldRefreshBearerToken(_timeProvider, _refreshLeadTime))
      {
        return;
      }

      if (string.IsNullOrWhiteSpace(auth.RefreshToken))
      {
        return;
      }

      var client = _httpClientFactory.CreateClient(ControlrApiClientNames.UnauthenticatedClient);
      using var response = await client.PostAsJsonAsync(
        RefreshEndpoint,
        new RefreshTokenRequestDto(auth.RefreshToken),
        cancellationToken);

      if (response.StatusCode == HttpStatusCode.Unauthorized)
      {
        throw new InvalidOperationException("The refresh token is no longer valid.");
      }

      await response.EnsureSuccessStatusCodeWithDetails();

      var tokens = await response.Content.ReadFromJsonAsync<AccessTokenResponseDto>(cancellationToken) ??
        throw new InvalidOperationException("The refresh response was empty.");

      auth.SetBearerTokenResponse(tokens, _timeProvider);
      if (State != ControlrAuthSessionState.Authenticated)
      {
        UpdateState(ControlrAuthSessionState.Authenticated);
      }
    }
    finally
    {
      auth.BearerRefreshLock.Release();
    }
  }

  private void ResetSession(bool clearPersonalAccessToken)
  {
    StopRefreshLoop();
    ClearPendingCredentials();
    _authState.ClearBearerTokens();

    if (clearPersonalAccessToken)
    {
      _authState.PersonalAccessToken = null;
    }
  }

  private async Task RunRefreshLoop(CancellationToken cancellationToken)
  {
    try
    {
      while (!cancellationToken.IsCancellationRequested)
      {
        var expiresAt = _authState.BearerTokenExpiresAt;
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
      await HandleRefreshLoopFault("The session expired. Sign in again.");
    }
  }

  private void StartRefreshLoop()
  {
    StopRefreshLoop();
    if (!_authState.CanRefreshBearerToken)
    {
      return;
    }

    _refreshLoopCts = new CancellationTokenSource();
    _ = RunRefreshLoop(_refreshLoopCts.Token);
  }

  private void StopRefreshLoop()
  {
    _refreshLoopCts?.Cancel();
    _refreshLoopCts?.Dispose();
    _refreshLoopCts = null;
  }

  private void UpdateState(ControlrAuthSessionState state, string? message = null)
  {
    _state = state;
    StateChanged?.Invoke(this, new ControlrAuthSessionStateChangedEventArgs(state, message));
  }
}