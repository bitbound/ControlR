using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz;
using ControlR.Web.Server.Components.Account;
using ControlR.Web.Server.Services.DeviceManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControlR.Web.Server.Startup;

public static class AuthorizationRegistrationExtensions
{
  public static void AddControlrAuthorization(this IHostApplicationBuilder hostBuilder)
  {
    hostBuilder.Services.AddCascadingAuthenticationState();
    hostBuilder.Services.AddScoped<IdentityRedirectManager>();
    hostBuilder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

    hostBuilder.Services.ConfigureApplicationCookie(options =>
    {
      options.Events.OnRedirectToLogin = context =>
      {
        // For API requests, return 401 instead of redirecting
        if (context.Request.Path.StartsWithSegments("/api"))
        {
          context.Response.StatusCode = StatusCodes.Status401Unauthorized;
          return Task.CompletedTask;
        }

        // For UI requests, redirect to the login page
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
      };

      options.Events.OnRedirectToAccessDenied = context =>
      {
        // For API requests, return 403 instead of redirecting
        if (context.Request.Path.StartsWithSegments("/api"))
        {
          context.Response.StatusCode = StatusCodes.Status403Forbidden;
          return Task.CompletedTask;
        }

        // For UI requests, redirect to the access-denied page
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
      };
    });

    hostBuilder.Services
      .AddAuthorizationBuilder()
      .SetDefaultPolicy(new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(CustomSchemes.Dynamic)
        .RequireAuthenticatedUser()
        .Build())
      .AddPolicy(RequireServerServiceAccountPolicy.PolicyName, RequireServerServiceAccountPolicy.Create())
      .AddPolicy(CombinedAuthorizationPolicies.RequireServerOrTenantAdminPolicy, CombinedAuthorizationPolicies.CreateServerOrTenantAdmin())
      .AddPolicy(CombinedAuthorizationPolicies.RequireServerOrTenantAdminOrInstallerKeyManagerPolicy, CombinedAuthorizationPolicies.CreateServerOrTenantAdminOrInstallerKeyManager())
      .AddPolicy(RequireServerAdministratorPolicy.PolicyName, RequireServerAdministratorPolicy.Create())
      .AddPolicy(DeviceAccessByDeviceResourcePolicy.PolicyName, DeviceAccessByDeviceResourcePolicy.Create());

    hostBuilder.Services.AddScoped<IAuthorizationHandler, ServiceProviderRequirementHandler>();
    hostBuilder.Services.AddScoped<IAuthorizationHandler, ServiceProviderAsyncRequirementHandler>();
    hostBuilder.Services.AddScoped<IDeviceAccessScopeResolver, DeviceAccessScopeResolver>();
  }
}