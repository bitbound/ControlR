using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ControlR.Web.Server.Tests.Helpers;

internal static class ControllerExtensions
{
  /// <summary>
  /// Sets up the user context for a controller in tests
  /// </summary>
  /// <param name="testApp">The test application instance</param>
  /// <param name="controller">The controller to configure</param>
  /// <param name="user">The user to set for authorization</param>
  /// <param name="roles">Optional roles to assign to the user</param>
  /// <returns>A task representing the async operation</returns>
  public static Task SetControllerUser(this ControllerBase controller, AppUser user, string[]? roles = null)
  {
    ArgumentNullException.ThrowIfNull(controller);
    ArgumentNullException.ThrowIfNull(user);    // Create list of claims
    var claims = new List<Claim>
    {
      new(ClaimTypes.NameIdentifier, user.Id.ToString()),
      new(ClaimTypes.Email, user.Email ?? string.Empty),
      new("TenantId", user.TenantId.ToString()),
      new(UserClaimTypes.TenantId, user.TenantId.ToString()),
      new(UserClaimTypes.UserId, user.Id.ToString())
    };

    // Add role claims if provided
    if (roles != null)
    {
      foreach (var role in roles)
      {
        claims.Add(new Claim(ClaimTypes.Role, role));
      }
    }

    // Create ClaimsIdentity with necessary claims
    var identity = new ClaimsIdentity(claims, "TestAuthentication");

    // Create ClaimsPrincipal
    var principal = new ClaimsPrincipal(identity);

    // Configure controller's HttpContext
    if (controller.ControllerContext.HttpContext == null)
    {
      controller.ControllerContext.HttpContext = new DefaultHttpContext
      {
        User = principal
      };
    }
    else
    {
      controller.ControllerContext.HttpContext.User = principal;
    }

    return Task.CompletedTask;
  }
}
