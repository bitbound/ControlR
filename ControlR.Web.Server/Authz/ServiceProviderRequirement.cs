using Microsoft.AspNetCore.Authorization;

namespace ControlR.Web.Server.Authz;

public class ServiceProviderRequirement(Func<IServiceProvider, AuthorizationHandlerContext, Task<bool>> assertion)
  : IAuthorizationRequirement
{
  public Func<IServiceProvider, AuthorizationHandlerContext, Task<bool>> Assertion { get; } = assertion;
}