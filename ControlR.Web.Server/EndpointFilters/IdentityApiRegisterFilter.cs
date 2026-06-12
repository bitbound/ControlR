using Microsoft.AspNetCore.Identity.Data;

namespace ControlR.Web.Server.EndpointFilters;

public class IdentityApiRegisterFilter : IEndpointFilter
{
  public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocationContext, EndpointFilterDelegate next)
  {
    var path = invocationContext.HttpContext.Request.Path.Value;
    if (path?.EndsWith("/register", StringComparison.OrdinalIgnoreCase) != true)
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

    var confirmationBaseUrl =
      $"{invocationContext.HttpContext.Request.Scheme}://{invocationContext.HttpContext.Request.Host}";

    var result = await userCreator.CreateUser(
      registerRequest.Email,
      registerRequest.Password,
      returnUrl: null,
      confirmationBaseUrl,
      isPublicRegistration: true,
      cancellationToken: invocationContext.HttpContext.RequestAborted);

    if (!result.Succeeded)
    {
      if (result.IdentityResult.Errors.Any(e => e.Code == "RegistrationDisabled"))
      {
        return Results.NotFound();
      }

      return Results.ValidationProblem(
        result.IdentityResult.Errors
          .GroupBy(e => string.IsNullOrWhiteSpace(e.Code) ? nameof(IdentityError) : e.Code)
          .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
    }

    return Results.Ok();
  }
}