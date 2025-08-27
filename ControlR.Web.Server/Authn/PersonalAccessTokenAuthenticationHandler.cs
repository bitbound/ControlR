using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace ControlR.Web.Server.Authn;

public class PersonalAccessTokenAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
  public const string DefaultScheme = "PersonalAccessToken";
  public const string DefaultHeaderName = "x-personal-token";
  public string Scheme => DefaultScheme;
  public string HeaderName { get; set; } = DefaultHeaderName;
}

public class PersonalAccessTokenAuthenticationHandler(
  UrlEncoder encoder,
  UserManager<AppUser> userManager,
  ILoggerFactory logger,
  IPersonalAccessTokenManager personalAccessTokenManager,
  IOptionsMonitor<PersonalAccessTokenAuthenticationSchemeOptions> options) : AuthenticationHandler<PersonalAccessTokenAuthenticationSchemeOptions>(options, logger, encoder)
{
  private readonly IPersonalAccessTokenManager _personalAccessTokenManager = personalAccessTokenManager;
  private readonly UserManager<AppUser> _userManager = userManager;

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

    var providedPat = authHeader.Trim();
    if (string.IsNullOrWhiteSpace(providedPat))
    {
      return AuthenticateResult.NoResult();
    }

    var validationResult = await _personalAccessTokenManager.ValidateToken(providedPat);
    if (!validationResult.IsSuccess || !validationResult.Value.IsValid)
    {
      return AuthenticateResult.Fail("Invalid personal access token");
    }

    var result = validationResult.Value;

    // Load the user by id and build a ClaimsPrincipal similar to other authentication handlers
    var user = await _userManager.FindByIdAsync(result.UserId.Value.ToString());
    if (user is null)
    {
      return AuthenticateResult.Fail("User not found for personal access token");
    }

    var claims = new List<Claim>
    {
      new(UserClaimTypes.UserId, user.Id.ToString()),
      new(UserClaimTypes.TenantId, user.TenantId.ToString()),
      new(ClaimTypes.NameIdentifier, user.Id.ToString()),
      new(ClaimTypes.Name, user.UserName ?? "User"),
      new(UserClaimTypes.AuthenticationMethod, PersonalAccessTokenAuthenticationSchemeOptions.DefaultScheme),
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

    return AuthenticateResult.Success(ticket);
  }
}
