namespace ControlR.Web.Server.Authz;

public class ServiceProviderRequirement(
  Func<IServiceProvider, AuthorizationHandlerContext, IAuthorizationHandler, bool> assertion)
  : IAuthorizationRequirement
{
  public Func<IServiceProvider, AuthorizationHandlerContext, IAuthorizationHandler, bool> Assertion { get; } =
    assertion;
}