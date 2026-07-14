using System.Security.Claims;
using System.Text.Encodings.Web;
using ControlR.Web.Server.Caching;
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
      var failureKey = CacheKeys.GetServiceAccountAuthFailure(remoteIp);

      if (memoryCache.TryGetValue<int>(failureKey, out var failureCount) &&
          failureCount >= appOptions.CurrentValue.ServiceAccountAuthFailureLimit)
      {
        return AuthenticateResult.Fail("Too many failed authentication attempts. Try again later.");
      }

      var validationResult = await serviceAccountManager.ValidateCredential(apiKey, Context.RequestAborted);
      if (!validationResult.IsSuccess)
      {
        memoryCache.Set(
          failureKey,
          failureCount + 1,
          TimeSpan.FromMinutes(appOptions.CurrentValue.ServiceAccountAuthFailureWindowMinutes));
        return AuthenticateResult.Fail("Invalid service account credential");
      }

      memoryCache.Remove(failureKey);

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
