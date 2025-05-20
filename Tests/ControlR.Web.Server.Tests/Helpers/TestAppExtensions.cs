using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Client.Authz;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace ControlR.Web.Server.Tests.Helpers;

public static class TestAppExtensions
{    /// <summary>
    /// Sets up the user context for a controller in tests
    /// </summary>
    /// <param name="testApp">The test application instance</param>
    /// <param name="controller">The controller to configure</param>
    /// <param name="user">The user to set for authorization</param>
    /// <param name="roles">Optional roles to assign to the user</param>
    /// <returns>A task representing the async operation</returns>
    public static Task SetControllerUser(this TestApp testApp, ControllerBase controller, AppUser user, string[]? roles = null)
    {
        if (controller == null)
            throw new ArgumentNullException(nameof(controller));
        
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        // Create list of claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim("TenantId", user.TenantId.ToString())
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

    /// <summary>
    /// Creates an instance of a controller with the necessary services injected from the TestApp
    /// </summary>
    /// <typeparam name="T">The controller type to create</typeparam>
    /// <param name="testApp">The test application instance</param>
    /// <returns>An instance of the controller</returns>
    public static T CreateController<T>(this TestApp testApp) where T : ControllerBase
    {
        var controller = (T)ActivatorUtilities.CreateInstance(testApp.Services, typeof(T));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = testApp.Services
            }
        };
        return controller;
    }
}
