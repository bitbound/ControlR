using ControlR.Libraries.Api.Contracts.Dtos.AgentApi;
using Microsoft.AspNetCore.Authentication;

namespace ControlR.Web.Server.Authn;

public class AgentSignatureAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
  public const string DefaultHeaderName = AgentSignatureHeader.Name;
  public const string DefaultScheme = "AgentSignature";

  public string HeaderName { get; set; } = DefaultHeaderName;
}
