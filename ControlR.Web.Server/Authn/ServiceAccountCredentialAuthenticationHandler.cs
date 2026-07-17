using System.Security.Claims;
using System.Text.Encodings.Web;
using ControlR.Web.Server.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Web.Server.Authn;

public class ServiceAccountCredentialAuthenticationHandler(
  UrlEncoder encoder,
  IMemoryCache memoryCache,
  IServiceAccountManager serviceAccountManager,
  ILoggerFactory logger,
  IOptionsMonitor<ServiceAccountCredentialAuthenticationSchemeOptions> options,
  IOptionsMonitor<AppOptions> appOptions,
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

      var remoteIp = Context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
      var credentialIdPrefix = apiKey.Split(':', 2).FirstOrDefault();
      var ipFailureKey = CacheKeys.GetServiceAccountAuthFailureByIp(remoteIp);
      var credentialFailureKey = CacheKeys.GetServiceAccountAuthFailureByCredential(credentialIdPrefix);
      var failureLimit = appOptions.CurrentValue.ServiceAccountAuthFailureLimit;
      var failureWindow = TimeSpan.FromMinutes(appOptions.CurrentValue.ServiceAccountAuthFailureWindowMinutes);

      // Two-axis throttling: independent limits per source IP and per credential.
      // Combined (IP, credential) keys would let an attacker rotate credential IDs
      // from one IP to bypass the limit; combined keys per axis let each axis
      // catch its own attack pattern.
      if (memoryCache.TryGetValue<int>(ipFailureKey, out var ipFailures) && ipFailures >= failureLimit)
      {
        return AuthenticateResult.Fail("Too many failed authentication attempts from this source. Try again later.");
      }

      if (memoryCache.TryGetValue<int>(credentialFailureKey, out var credentialFailures) && credentialFailures >= failureLimit)
      {
        return AuthenticateResult.Fail("Too many failed authentication attempts for this credential. Try again later.");
      }

      var validationResult = await serviceAccountManager.ValidateCredential(apiKey, Context.RequestAborted);
      if (!validationResult.IsSuccess)
      {
        memoryCache.Set(ipFailureKey, ipFailures + 1, failureWindow);
        memoryCache.Set(credentialFailureKey, credentialFailures + 1, failureWindow);
        return AuthenticateResult.Fail("Invalid service account credential");
      }

      memoryCache.Remove(ipFailureKey);
      memoryCache.Remove(credentialFailureKey);

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
      var credentialId = apiKey?.Split(':', 2).FirstOrDefault() ?? "unknown";
      var remoteIp = Context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
      handlerLogger.LogError(ex, "Service account credential authentication failed. Credential prefix: {CredentialId}, Remote IP: {RemoteIp}", credentialId, remoteIp);
      return AuthenticateResult.Fail("An internal error occurred during authentication");
    }
  }
}
