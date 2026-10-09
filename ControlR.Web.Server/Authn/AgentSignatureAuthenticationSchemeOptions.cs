using ControlR.Libraries.Api.Contracts.Dtos.AgentApi;
using Microsoft.AspNetCore.Authentication;

namespace ControlR.Web.Server.Authn;

public class AgentSignatureAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
  public const string DefaultHeaderName = AgentSignatureHeader.Name;

  /// <summary>
  /// The policy an agent-only endpoint pins, because <see cref="PolicyNames"/> holds only the
  /// permission-based policies and an agent carries no permissions.
  /// </summary>
  public const string DefaultPolicy = "RequireInstalledAgent";

  public const string DefaultScheme = "AgentSignature";

  public string HeaderName { get; set; } = DefaultHeaderName;
}
