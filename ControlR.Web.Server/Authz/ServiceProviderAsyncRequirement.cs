namespace ControlR.Web.Server.Authz;

public class ServiceProviderAsyncRequirement(
  Func<IServiceProvider, AuthorizationHandlerContext, IAuthorizationHandler, Task<bool>> assertion)
  : IAuthorizationRequirement
{
  public Func<IServiceProvider, AuthorizationHandlerContext, IAuthorizationHandler, Task<bool>> Assertion { get; } =
    assertion;
}