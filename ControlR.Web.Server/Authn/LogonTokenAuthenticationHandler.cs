using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using ControlR.Web.Server.Services.LogonTokens;

namespace ControlR.Web.Server.Authn;

public class LogonTokenAuthenticationHandler(
  UrlEncoder encoder,
  UserManager<AppUser> userManager,
  SignInManager<AppUser> signInManager,
  IOptionsMonitor<LogonTokenAuthenticationSchemeOptions> options,
  ILoggerFactory logger,
  ILogonTokenProvider logonTokenProvider) : AuthenticationHandler<LogonTokenAuthenticationSchemeOptions>(options, logger, encoder)
{
  private readonly ILogonTokenProvider _logonTokenProvider = logonTokenProvider;
  private readonly UserManager<AppUser> _userManager = userManager;
  private readonly SignInManager<AppUser> _signInManager = signInManager;

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
      return AuthenticateResult.Fail("Valid device ID is required with logon token");
    }

    var tokenValidation = await _logonTokenProvider.ValidateAndConsumeTokenAsync(
      $"{tokenValue}", 
      deviceId);

    if (!tokenValidation.IsValid)
    {
      return AuthenticateResult.Fail(tokenValidation.ErrorMessage ?? "Invalid logon token");
    }

    // Load the real user from the database to get all their properties and roles
    var user = await _userManager.FindByIdAsync(tokenValidation.UserId!.Value.ToString());
    if (user is null)
    {
      return AuthenticateResult.Fail("User not found for logon token");
    }

    var claims = new List<Claim>
    {
      new(ClaimTypes.NameIdentifier, user.Id.ToString()),
      new(ClaimTypes.Name, user.UserName ?? "User"),
      new(UserClaimTypes.DeviceId, deviceId.ToString()),
      new(UserClaimTypes.AuthenticationMethod, "LogonToken"),
      new(UserClaimTypes.TenantId, user.TenantId.ToString())
    };

    if (!string.IsNullOrWhiteSpace(user.Email))
    {
      claims.Add(new Claim(ClaimTypes.Email, user.Email));
    }

    // Add role claims from user manager
    var roles = await _userManager.GetRolesAsync(user);
    foreach (var role in roles)
    {
      claims.Add(new Claim(ClaimTypes.Role, role));
    }

    var identity = new ClaimsIdentity(claims, Scheme.Name);
    var principal = new ClaimsPrincipal(identity);
    var ticket = new AuthenticationTicket(principal, Scheme.Name);

    await _signInManager.SignInAsync(user, isPersistent: false, authenticationMethod: IdentityConstants.ApplicationScheme);

    return AuthenticateResult.Success(ticket);
  }
}
