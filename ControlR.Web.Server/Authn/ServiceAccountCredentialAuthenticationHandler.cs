using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;

namespace ControlR.Web.Server.Authn;

public class ServiceAccountCredentialAuthenticationHandler(
  UrlEncoder encoder,
  IServiceAccountManager serviceAccountManager,
  ILoggerFactory logger,
  IOptionsMonitor<ServiceAccountCredentialAuthenticationSchemeOptions> options,
  ILogger<ServiceAccountCredentialAuthenticationHandler> handlerLogger) : AuthenticationHandler<ServiceAccountCredentialAuthenticationSchemeOptions>(options, logger, encoder)
{
  protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    string? apiKey = null;
    try
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

      apiKey = authHeader.Trim();
      if (string.IsNullOrWhiteSpace(apiKey))
      {
        return AuthenticateResult.NoResult();
      }

      var validationResult = await serviceAccountManager.ValidateCredential(apiKey, Context.RequestAborted);
      if (!validationResult.IsSuccess)
      {
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
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      var credentialPrefix = apiKey?.Split(':', 2).FirstOrDefault() ?? "unknown";
      var remoteIp = Context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
      handlerLogger.LogError(ex, "Service account credential authentication failed. Credential prefix: {CredentialPrefix}, Remote IP: {RemoteIp}", credentialPrefix, remoteIp);
      return AuthenticateResult.Fail("An internal error occurred during authentication");
    }
  }
}
