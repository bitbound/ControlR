using Microsoft.AspNetCore.Authorization;

namespace ControlR.Web.Server.Authz;

public class ServiceProviderRequirementHandler(IServiceProvider serviceProvider) : IAuthorizationHandler
{
  private readonly IServiceProvider _serviceProvider = serviceProvider;

  public async Task HandleAsync(AuthorizationHandlerContext context)
  {
    var requirements = context.Requirements
      .OfType<ServiceProviderRequirement>()
      .ToAsyncEnumerable();

    await foreach (var requirement in requirements)
    {
      if (requirement.Assertion.Invoke(_serviceProvider, context))
      {
        context.Succeed(requirement);
      }
    }
  }
}