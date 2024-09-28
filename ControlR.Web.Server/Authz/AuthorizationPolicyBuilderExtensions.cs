using Microsoft.AspNetCore.Authorization;

namespace ControlR.Web.Server.Authz;

public static class AuthorizationPolicyBuilderExtensions
{
  public static AuthorizationPolicyBuilder RequireServiceProviderAssertion(
    this AuthorizationPolicyBuilder builder,
    Func<IServiceProvider, AuthorizationHandlerContext, Task<bool>> assertion)
  {
    builder.Requirements.Add(new ServiceProviderRequirement(assertion));
    return builder;
  }
}