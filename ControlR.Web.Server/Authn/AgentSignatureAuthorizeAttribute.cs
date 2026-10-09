namespace ControlR.Web.Server.Authn;

public class AgentSignatureAuthorizeAttribute : AuthorizeAttribute
{
  public AgentSignatureAuthorizeAttribute()
  {
    Policy = AgentSignatureAuthenticationSchemeOptions.DefaultPolicy;
  }
}