using Microsoft.AspNetCore.Authorization;

namespace ControlR.Web.Server.Authz;

public class ServiceProviderRequirement(Func<IServiceProvider, AuthorizationHandlerContext, bool> assertion)
  : IAuthorizationRequirement
{
  public Func<IServiceProvider, AuthorizationHandlerContext, bool> Assertion { get; } = assertion;
}