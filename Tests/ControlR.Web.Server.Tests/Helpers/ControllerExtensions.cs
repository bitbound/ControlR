using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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
  public static async Task SetControllerUser(
    this ControllerBase controller,
    AppUser user,
    UserManager<AppUser> userManager)
  {
    ArgumentNullException.ThrowIfNull(controller);
    ArgumentNullException.ThrowIfNull(user);

    var userRoles = await userManager.GetRolesAsync(user);
    var userClaims = await userManager.GetClaimsAsync(user);
    var roleClaims = userRoles.Select(role => new Claim(ClaimTypes.Role, role));

    Claim[] claims =
    [
      new(ClaimTypes.NameIdentifier, $"{user.Id}"),
      new(ClaimTypes.Name, $"{user.UserName}"),
      new(ClaimTypes.Email, $"{user.Email}"),
      new(UserClaimTypes.TenantId, user.TenantId.ToString()),
      new(UserClaimTypes.UserId, user.Id.ToString()),
      ..roleClaims,
      ..userClaims
    ];

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
  }
}
