using Microsoft.AspNetCore.Authentication;

namespace ControlR.Web.Server.Authn;

public class ServiceAccountCredentialAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
  public const string DefaultHeaderName = "x-api-key";
  public const string DefaultScheme = "ServiceAccountCredential";

  public string HeaderName { get; set; } = DefaultHeaderName;
  public string Scheme => DefaultScheme;
}