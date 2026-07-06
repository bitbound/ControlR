using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using ControlR.Web.Server.Services.ServiceAccounts;

namespace ControlR.Web.Server.Authn;

public class ServiceAccountCredentialAuthenticationHandler(
  UrlEncoder encoder,
  IServiceAccountManager serviceAccountManager,
  ILoggerFactory logger,
  IOptionsMonitor<ServiceAccountCredentialAuthenticationSchemeOptions> options) : AuthenticationHandler<ServiceAccountCredentialAuthenticationSchemeOptions>(options, logger, encoder)
{
  protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    if (!Request.Headers.TryGetValue(Options.HeaderName, out var authHeaderValues))
    {
      return AuthenticateResult.NoResult();
    }

    var authHeader = authHeaderValues.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(authHeader))
    {
      return AuthenticateResult.NoResult();
    }

    var apiKey = authHeader.Trim();
    if (string.IsNullOrWhiteSpace(apiKey))
    {
      return AuthenticateResult.NoResult();
    }

    var validationResult = await serviceAccountManager.ValidateCredential(apiKey, Context.RequestAborted);
    if (!validationResult.IsSuccess)
    {
      if (validationResult.HadException)
      {
        return AuthenticateResult.Fail(validationResult.Exception!);
      }

      return AuthenticateResult.Fail("Invalid service account credential");
    }

    var (account, credential) = validationResult.Value;

    var claims = new List<Claim>
    {
      new(PrincipalClaimTypes.PrincipalType, PrincipalClaimTypes.ServerServiceAccount),
      new(PrincipalClaimTypes.PrincipalId, account.Id.ToString()),
      new(UserClaimTypes.AuthenticationMethod, PrincipalClaimTypes.ServiceAccountCredentialMethod),
      new(PrincipalClaimTypes.CredentialId, credential.Id.ToString())
    };

    if (account.Description is not null)
    {
      claims.Add(new Claim(ClaimTypes.Name, account.Description));
    }

    var identity = new ClaimsIdentity(claims, Scheme.Name);
    var principal = new ClaimsPrincipal(identity);
    var ticket = new AuthenticationTicket(principal, Scheme.Name);

    return AuthenticateResult.Success(ticket);
  }
}