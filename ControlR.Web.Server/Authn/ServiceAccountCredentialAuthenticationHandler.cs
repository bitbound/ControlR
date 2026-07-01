using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using ControlR.Web.Server.Services.ServiceAccounts;

namespace ControlR.Web.Server.Authn;

public class ServiceAccountCredentialAuthenticationHandler(
  UrlEncoder encoder,
  IServiceAccountManager serviceAccountManager,
  ILoggerFactory logger,
  IOptionsMonitor<ServiceAccountCredentialAuthenticationSchemeOptions> options) : AuthenticationHandler<ServiceAccountCredentialAuthenticationSchemeOptions>(options, logger, encoder)
{
  private const int MaxFailures = 5;
  private static readonly MemoryCache _failureCache = new(new MemoryCacheOptions());
  private static readonly TimeSpan _failureWindow = TimeSpan.FromMinutes(5);

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

    // Rate limiting keyed by the credential id part (or remote IP fallback) to match the
    // PAT handler's approach.
    var keyPart = apiKey.Split(':', 2).FirstOrDefault() ?? "unknown";
    var remoteIp = Context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var failureKey = $"sacredfail:{keyPart}:{remoteIp}";
    if (_failureCache.TryGetValue<int>(failureKey, out var failureCount) && failureCount >= MaxFailures)
    {
      return AuthenticateResult.Fail("Too many failed credential attempts. Try again later.");
    }

    var validationResult = await serviceAccountManager.ValidateCredentialAsync(apiKey, Context.RequestAborted);
    if (!validationResult.IsSuccess)
    {
      var newCount = failureCount + 1;
      _failureCache.Set(failureKey, newCount, _failureWindow);
      return AuthenticateResult.Fail("Invalid service account credential");
    }

    _failureCache.Remove(failureKey);

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