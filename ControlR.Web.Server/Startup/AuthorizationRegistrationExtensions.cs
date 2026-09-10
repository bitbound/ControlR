using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Components.Account;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.Authorization.Capabilities;
using ControlR.Web.Server.Services.DeviceManagement;
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

    var authorizationBuilder = hostBuilder.Services
      .AddAuthorizationBuilder()
      .SetDefaultPolicy(new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(CustomSchemes.Dynamic)
        .RequireAuthenticatedUser()
        .Build());

    foreach (var (policyName, definition) in PermissionPolicies.Definitions)
    {
      authorizationBuilder.AddPolicy(policyName, policy => policy
        .AddAuthenticationSchemes(CustomSchemes.Dynamic)
        .RequireAuthenticatedUser()
        .RequirePermission(definition.PermissionName, definition.ResourceScopeKind));
    }

    foreach (var (policyName, permissionName) in DeviceResourcePolicies.PolicyToPermission)
    {
      authorizationBuilder.AddPolicy(policyName, policy => policy
        .AddAuthenticationSchemes(CustomSchemes.Dynamic)
        .RequireAuthenticatedUser()
        .RequirePermission(permissionName, PermissionScopeKind.Device));
    }

    hostBuilder.Services.AddScoped<IAuthorizationHandler, PermissionRequirementHandler>();
    hostBuilder.Services.AddScoped<IDeviceAccessScopeResolver, DeviceAccessScopeResolver>();
    hostBuilder.Services.AddSingleton<IDesktopSessionAccessAuthorizer, DesktopSessionAccessAuthorizer>();
    hostBuilder.Services.AddScoped<IResourceDescriptorFactory, ResourceDescriptorFactory>();
    hostBuilder.Services.AddScoped<IPermissionEvaluationContextLoader, PermissionEvaluationContextLoader>();
    hostBuilder.Services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();
    hostBuilder.Services.AddScoped<IDeviceAuthorizationService, DeviceAuthorizationService>();
    hostBuilder.Services.AddScoped<ICredentialScopeService, CredentialScopeService>();
    hostBuilder.Services.AddSingleton<IAuthorizationChangeLogFactory, AuthorizationChangeLogFactory>();
  }
}