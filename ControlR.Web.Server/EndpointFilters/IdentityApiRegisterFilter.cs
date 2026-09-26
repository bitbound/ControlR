using ControlR.Web.Server.Services.Users;
using Microsoft.AspNetCore.Identity.Data;

namespace ControlR.Web.Server.EndpointFilters;

public class IdentityApiRegisterFilter : IEndpointFilter
{
  public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocationContext, EndpointFilterDelegate next)
  {
    // Routing ignores a trailing slash, so "/api/auth/register/" selects the same endpoint and reaches
    // this filter. Without normalizing, that request falls through to the framework's own handler, which
    // knows nothing about the registration gate.
    var path = invocationContext.HttpContext.Request.Path.Value?.TrimEnd('/');
    if (path is null || !path.EndsWith("/register", StringComparison.OrdinalIgnoreCase))
    {
      return await next(invocationContext);
    }

    var userCreator = invocationContext.HttpContext.RequestServices
      .GetRequiredService<Services.Users.IUserCreator>();

    var registerRequest = invocationContext.GetArgument<RegisterRequest>(0);
    if (registerRequest is null)
    {
      return Results.Problem("Invalid registration request.", statusCode: StatusCodes.Status400BadRequest);
    }

    var result = await userCreator.CreateUser(
      registerRequest.Email,
      registerRequest.Password,
      returnUrl: null,
      isPublicRegistration: true,
      cancellationToken: invocationContext.HttpContext.RequestAborted);

    if (!result.Succeeded)
    {
      if (result.IdentityResult.Errors.Any(e => e.Code == UserCreator.RegistrationDisabledErrorCode))
      {
        return Results.NotFound();
      }

      if (result.IdentityResult.Errors.Any(e => e.Code == UserCreator.ConfirmationEmailUnavailableErrorCode))
      {
        return Results.Problem(
          "This server cannot send confirmation emails. Contact an administrator.",
          statusCode: StatusCodes.Status503ServiceUnavailable);
      }

      return Results.ValidationProblem(
        result.IdentityResult.Errors
          .GroupBy(e => string.IsNullOrWhiteSpace(e.Code) ? nameof(IdentityError) : e.Code)
          .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
    }

    return Results.Ok();
  }
}