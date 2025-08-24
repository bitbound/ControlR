using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using ControlR.Web.Server.Services.LogonTokens;

namespace ControlR.Web.Server.Authn;

public class LogonTokenAuthenticationHandler : AuthenticationHandler<LogonTokenAuthenticationSchemeOptions>
{
  private readonly ILogonTokenProvider _logonTokenProvider;

  public LogonTokenAuthenticationHandler(
    IOptionsMonitor<LogonTokenAuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ILogonTokenProvider logonTokenProvider)
    : base(options, logger, encoder)
  {
    _logonTokenProvider = logonTokenProvider;
  }

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
      tokenValue!, 
      deviceId);

    if (!tokenValidation.IsValid)
    {
      return AuthenticateResult.Fail(tokenValidation.ErrorMessage ?? "Invalid logon token");
    }

    var claims = new List<Claim>
    {
      new(ClaimTypes.NameIdentifier, $"{tokenValidation.UserId}"),
      new(ClaimTypes.Name, tokenValidation.UserName ?? "External User"),
      new(UserClaimTypes.DeviceId, deviceId.ToString()),
      new(UserClaimTypes.AuthenticationMethod, "LogonToken"),
      new(UserClaimTypes.TenantId, $"{tokenValidation.TenantId}")
    };

    if (!string.IsNullOrWhiteSpace(tokenValidation.Email))
    {
      claims.Add(new Claim(ClaimTypes.Email, tokenValidation.Email));
    }

    var identity = new ClaimsIdentity(claims, Scheme.Name);
    var principal = new ClaimsPrincipal(identity);
    var ticket = new AuthenticationTicket(principal, Scheme.Name);

    return AuthenticateResult.Success(ticket);
  }
}
