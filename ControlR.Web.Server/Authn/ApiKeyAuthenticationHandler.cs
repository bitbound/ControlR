using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace ControlR.Web.Server.Authn;

public class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
  public const string DefaultScheme = "ApiKey";
  public string Scheme => DefaultScheme;
  public string HeaderName { get; set; } = "x-api-key";
}

public class ApiKeyAuthenticationHandler(
  IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
  ILoggerFactory logger,
  UrlEncoder encoder,
  IApiKeyManager apiKeyManager) : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>(options, logger, encoder)
{
  private readonly IApiKeyManager _apiKeyManager = apiKeyManager;

  protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    if (!Request.Headers.TryGetValue(Options.HeaderName, out var apiKeyHeaderValues))
    {
      return AuthenticateResult.NoResult();
    }

    var providedApiKey = apiKeyHeaderValues.FirstOrDefault();

    if (string.IsNullOrWhiteSpace(providedApiKey))
    {
      return AuthenticateResult.NoResult();
    }

    var validationResult = await _apiKeyManager.ValidateApiKey(providedApiKey);
    if (!validationResult.IsSuccess)
    {
      return AuthenticateResult.Fail("Invalid API key");
    }

    var tenantId = validationResult.Value;

    var claims = new[]
    {
      new Claim(ClaimTypes.AuthenticationMethod, ApiKeyAuthenticationSchemeOptions.DefaultScheme),
      new Claim(ClaimTypes.Role, RoleNames.TenantAdministrator),
      new Claim(UserClaimTypes.TenantId, tenantId.ToString())
    };
    
    var identity = new ClaimsIdentity(claims, Scheme.Name);
    var principal = new ClaimsPrincipal(identity);
    var ticket = new AuthenticationTicket(principal, Scheme.Name);

    return AuthenticateResult.Success(ticket);
  }
}
