using Microsoft.AspNetCore.Authentication;

namespace ControlR.Web.Server.Authn;

public class PersonalAccessTokenAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
  public const string DefaultHeaderName = "x-personal-token";
  public const string DefaultScheme = "PersonalAccessToken";

  public string HeaderName { get; set; } = DefaultHeaderName;
  public string Scheme => DefaultScheme;
}
