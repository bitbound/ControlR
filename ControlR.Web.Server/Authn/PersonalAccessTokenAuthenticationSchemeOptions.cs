using Microsoft.AspNetCore.Authentication;

namespace ControlR.Web.Server.Authn;

public class PersonalAccessTokenAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
  public const string DefaultScheme = "PersonalAccessToken";
  public const string DefaultHeaderName = "x-personal-token";
  public string Scheme => DefaultScheme;
  public string HeaderName { get; set; } = DefaultHeaderName;
}
