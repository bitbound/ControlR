using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace ControlR.Web.Server.Tests.Helpers;

internal static class TestAppExtensions
{    
  /// <summary>
  /// Creates an instance of a controller with the necessary services injected from the TestApp
  /// </summary>
  /// <typeparam name="T">The controller type to create</typeparam>
  /// <param name="testApp">The test application instance</param>
  /// <returns>An instance of the controller</returns>
  public static T CreateController<T>(this TestApp testApp) where T : ControllerBase
  {
    var controller = ActivatorUtilities.CreateInstance<T>(testApp.Services);
    controller.ControllerContext = new ControllerContext
    {
      HttpContext = new DefaultHttpContext
      {
        RequestServices = testApp.Services
      }
    };
    return controller;
  }

  /// <summary>
  /// Creates a test tenant and saves it to the database
  /// </summary>
  /// <param name="testApp">The test application instance</param>
  /// <param name="tenantName">Optional tenant name, defaults to "Test Tenant"</param>
  /// <returns>The created tenant</returns>
  public static async Task<Tenant> CreateTestTenant(this TestApp testApp, string tenantName = "Test Tenant")
  {
    using var db = testApp.App.Services.GetRequiredService<AppDb>();
    
    var tenant = new Tenant { Id = Guid.NewGuid(), Name = tenantName };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();
    
    return tenant;
  }

  /// <summary>
  /// Creates a test user with the specified role and saves it to the database
  /// </summary>
  /// <param name="testApp">The test application instance</param>
  /// <param name="tenantId">The tenant ID for the user</param>
  /// <param name="email">Optional email, defaults to "test@example.com"</param>
  /// <param name="roles">Optional roles to assign to the user</param>
  /// <returns>The created user</returns>
  public static async Task<AppUser> CreateTestUser(
    this TestApp testApp, 
    Guid tenantId, 
    string email = "test@example.com", 
    params string[] roles)
  {
    var userCreator = testApp.App.Services.GetRequiredService<IUserCreator>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    
    var userResult = await userCreator.CreateUser(email, "T3stP@ssw0rd!", tenantId);
    if (!userResult.Succeeded)
    {
      throw new InvalidOperationException($"Failed to create test user: {string.Join(", ", userResult.IdentityResult.Errors.Select(e => e.Description))}");
    }
    
    var user = userResult.User;
    
    // Add roles if specified
    foreach (var role in roles)
    {
      var addResult = await userManager.AddToRoleAsync(user, role);
      if (!addResult.Succeeded)
      {
        throw new InvalidOperationException($"Failed to add role {role} to user: {string.Join(", ", addResult.Errors.Select(e => e.Description))}");
      }
    }
    
    return user;
  }

  /// <summary>
  /// Creates a controller configured with a test user context
  /// </summary>
  /// <typeparam name="T">The controller type to create</typeparam>
  /// <param name="testApp">The test application instance</param>
  /// <param name="user">The user to configure for the controller</param>
  /// <returns>The configured controller instance</returns>
  public static async Task<T> CreateControllerWithUser<T>(this TestApp testApp, AppUser user) where T : ControllerBase
  {
    var controller = testApp.CreateController<T>();
    var userManager = testApp.App.Services.GetRequiredService<UserManager<AppUser>>();
    
    await controller.SetControllerUser(user, userManager);
    
    return controller;
  }

  /// <summary>
  /// Creates a test tenant and user, then returns a controller configured with that user
  /// </summary>
  /// <typeparam name="T">The controller type to create</typeparam>
  /// <param name="testApp">The test application instance</param>
  /// <param name="tenantName">Optional tenant name</param>
  /// <param name="userEmail">Optional user email</param>
  /// <param name="roles">Optional roles to assign to the user</param>
  /// <returns>A tuple containing the controller, tenant, and user</returns>
  public static async Task<(T controller, Tenant tenant, AppUser user)> CreateControllerWithTestData<T>(
    this TestApp testApp,
    string tenantName = "Test Tenant",
    string userEmail = "test@example.com",
    params string[] roles) where T : ControllerBase
  {
    var tenant = await testApp.CreateTestTenant(tenantName);
    var user = await testApp.CreateTestUser(tenant.Id, userEmail, roles);
    var controller = await testApp.CreateControllerWithUser<T>(user);
    
    return (controller, tenant, user);
  }
}
