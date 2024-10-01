using Microsoft.AspNetCore.Authorization;

namespace ControlR.Web.Server.Authz;

public class ServiceProviderAsyncRequirementHandler(IServiceProvider serviceProvider) : IAuthorizationHandler
{
  private readonly IServiceProvider _serviceProvider = serviceProvider;

  public async Task HandleAsync(AuthorizationHandlerContext context)
  {
    foreach (var requirement in context.Requirements.OfType<ServiceProviderAsyncRequirement>())
    {
      if (await requirement.Assertion.Invoke(_serviceProvider, context))
      {
        context.Succeed(requirement);
      }
    }
  }
}