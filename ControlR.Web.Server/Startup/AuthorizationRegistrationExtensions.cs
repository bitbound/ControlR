using ControlR.Web.Client;
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

        // The Identity pages render as not-found when the main UI is disabled, so a login
        // redirect would dead-end. The query string is dropped so a logonToken is never echoed.
        var disableMainUi = context.HttpContext.RequestServices
          .GetRequiredService<IOptions<AppOptions>>()
          .Value.DisableMainUi;
        if (disableMainUi)
        {
          context.Response.Redirect(ClientRoutes.Unauthorized);
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
        .RequireAnyPermission(definition.PermissionNames, definition.ResourceScopeKind));
    }

    foreach (var (policyName, permissionName) in DeviceResourcePolicies.PolicyToPermission)
    {
      authorizationBuilder.AddPolicy(policyName, policy => policy
        .AddAuthenticationSchemes(CustomSchemes.Dynamic)
        .RequireAuthenticatedUser()
        .RequirePermission(permissionName, PermissionScopeKind.Device));
    }

    // An installed agent proves itself with a request signature instead of a permission, so this
    // policy names the agent scheme directly and asks only for authentication. The dynamic scheme
    // would otherwise forward these requests to the cookie handler.
    authorizationBuilder.AddPolicy(AgentSignatureAuthenticationSchemeOptions.DefaultPolicy, policy => policy
      .AddAuthenticationSchemes(AgentSignatureAuthenticationSchemeOptions.DefaultScheme)
      .RequireAuthenticatedUser());

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