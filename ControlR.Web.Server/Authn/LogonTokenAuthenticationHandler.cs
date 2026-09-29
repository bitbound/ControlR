using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using ControlR.Web.Server.Services.LogonTokens;

namespace ControlR.Web.Server.Authn;

public class LogonTokenAuthenticationHandler(
  UrlEncoder encoder,
  UserManager<AppUser> userManager,
  TimeProvider timeProvider,
  IOptionsMonitor<LogonTokenAuthenticationSchemeOptions> options,
  ILoggerFactory logger,
  ILogonTokenProvider logonTokenProvider) : AuthenticationHandler<LogonTokenAuthenticationSchemeOptions>(options, logger, encoder)
{
  private readonly ILogonTokenProvider _logonTokenProvider = logonTokenProvider;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly UserManager<AppUser> _userManager = userManager;

  protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    if (!Request.Query.TryGetValue("logonToken", out var tokenValue) ||
        string.IsNullOrWhiteSpace(tokenValue))
    {
      return AuthenticateResult.NoResult();
    }

    if (!Request.Query.TryGetValue("deviceId", out var deviceIdValue) ||
        string.IsNullOrWhiteSpace(deviceIdValue) ||
        !Guid.TryParse(deviceIdValue, out var deviceId))
    {
      return AuthenticateResult.Fail("Valid device ID is required with logon token.");
    }

    var tokenValidation = await _logonTokenProvider.ValidateAndConsumeToken(
      $"{tokenValue}",
      deviceId);

    if (!tokenValidation.IsValid)
    {
      return AuthenticateResult.Fail(tokenValidation.ErrorMessage ?? "Invalid logon token.");
    }

    if (!tokenValidation.UserId.HasValue)
    {
      return AuthenticateResult.Fail("User ID is required for logon token.");
    }

    var user = await _userManager.FindByIdAsync(tokenValidation.UserId.Value.ToString());
    if (user is null)
    {
      return AuthenticateResult.Fail("User not found for logon token.");
    }

    // Check lockout status — matches the guard in PersonalAccessTokenAuthenticationHandler.
    if (await _userManager.IsLockedOutAsync(user))
    {
      return AuthenticateResult.Fail("User account is locked");
    }

    var claims = new List<Claim>
    {
      new(UserClaimTypes.UserId, user.Id.ToString()),
      new(UserClaimTypes.TenantId, user.TenantId.ToString()),
      new(ClaimTypes.NameIdentifier, user.Id.ToString()),
      new(ClaimTypes.Name, user.UserName ?? "User"),
      new(UserClaimTypes.AuthenticationMethod, PrincipalClaimValues.LogonTokenMethod),
      new(UserClaimTypes.DeviceSessionScope, deviceId.ToString()),
      new(PrincipalClaimTypes.PrincipalType, PrincipalClaimValues.User),
      new(PrincipalClaimTypes.PrincipalId, user.Id.ToString()),
      new(PrincipalClaimTypes.CredentialId, tokenValidation.TokenId.Value.ToString()),
      new(PrincipalClaimTypes.CredentialType, PrincipalClaimValues.LogonTokenCredentialType),
    };

    if (!string.IsNullOrWhiteSpace(user.Email))
    {
      claims.Add(new Claim(ClaimTypes.Email, user.Email));
    }

    if (!string.IsNullOrWhiteSpace(tokenValidation.SessionCorrelationId))
    {
      claims.Add(new(UserClaimTypes.SessionCorrelationId, tokenValidation.SessionCorrelationId));
    }

    foreach (var sessionId in tokenValidation.AllowedDesktopSessionIds ?? [])
    {
      claims.Add(new(
        UserClaimTypes.AllowedDesktopSessionId,
        sessionId.ToString(CultureInfo.InvariantCulture)));
    }

    if (tokenValidation.AllowedDesktopSessionIds is not null)
    {
      claims.Add(new(UserClaimTypes.DesktopSessionRestriction, bool.TrueString));
    }

    try
    {
      user.LastLogin = _timeProvider.GetUtcNow();
      var updateResult = await _userManager.UpdateAsync(user);
      if (!updateResult.Succeeded)
      {
        Logger.LogWarning(
          "Failed to update LastLogin for user {UserId} during logon token auth: {Errors}",
          user.Id, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
      }
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "Failed to update LastLogin for user {UserId} during logon token auth.", user.Id);
    }

    var identity = new ClaimsIdentity(claims, Scheme.Name);
    var principal = new ClaimsPrincipal(identity);
    var ticket = new AuthenticationTicket(principal, Scheme.Name);

    // The application cookie's own Lifetime governs the session. Binding it to the token's
    // ExpiresAt would evict an idle technician mid-session, and consumed tokens cannot renew it.
    var cookieProperties = new AuthenticationProperties
    {
      IsPersistent = true
    };
    await Context.SignInAsync(IdentityConstants.ApplicationScheme, principal, cookieProperties);

    return AuthenticateResult.Success(ticket);
  }
}
