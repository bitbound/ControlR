namespace ControlR.Web.Server.Authz;

public static class AuthorizationPolicyBuilderExtensions
{
  public static AuthorizationPolicyBuilder RequireServiceProviderAssertion(
    this AuthorizationPolicyBuilder builder,
    Func<IServiceProvider, AuthorizationHandlerContext, IAuthorizationHandler, Task<bool>> assertion)
  {
    builder.Requirements.Add(new ServiceProviderAsyncRequirement(assertion));
    return builder;
  }

  public static AuthorizationPolicyBuilder RequireServiceProviderAssertion(
    this AuthorizationPolicyBuilder builder,
    Func<IServiceProvider, AuthorizationHandlerContext, IAuthorizationHandler, bool> assertion)
  {
    builder.Requirements.Add(new ServiceProviderRequirement(assertion));
    return builder;
  }
}