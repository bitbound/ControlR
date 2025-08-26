using Microsoft.AspNetCore.Authentication;

namespace ControlR.Web.Server.Authn;

public class LogonTokenAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
  public const string DefaultScheme = "LogonToken";
}
