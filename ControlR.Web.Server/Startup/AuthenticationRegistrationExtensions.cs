using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Components.Account;
using ControlR.Web.Server.Services.Users;
using Microsoft.AspNetCore.Authentication.BearerToken;

namespace ControlR.Web.Server.Startup;

public static class AuthenticationRegistrationExtensions
{
  public static void AddControlrAuthentication(
    this IHostApplicationBuilder hostBuilder,
    AppOptions appOptions)
  {
    hostBuilder.Services
      .AddIdentityApiEndpoints<AppUser>(options =>
      {
        options.User.RequireUniqueEmail = appOptions.RequireUserUniqueEmail;
        options.SignIn.RequireConfirmedEmail = appOptions.RequireUserEmailConfirmation;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
      })
      .AddEntityFrameworkStores<AppDb>()
      .AddSignInManager()
      .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
      .AddDefaultTokenProviders();

    if (appOptions.EnableInteractiveBearerLogin)
    {
      hostBuilder.Services.Configure<BearerTokenOptions>(IdentityConstants.BearerScheme, options =>
      {
        options.BearerTokenExpiration = TimeSpan.FromMinutes(appOptions.InteractiveBearerTokenExpirationMinutes);
        options.RefreshTokenExpiration = TimeSpan.FromDays(appOptions.InteractiveRefreshTokenExpirationDays);
      });
    }

    var authBuilder = hostBuilder.Services
      .AddAuthentication(options =>
      {
        options.DefaultScheme = CustomSchemes.Dynamic;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
      })
      .AddPolicyScheme(CustomSchemes.Dynamic, "Dynamic Authentication Scheme", options =>
      {
        options.ForwardDefaultSelector = context =>
        {
          // Check for logon token first (for device access integration)
          if (context.Request.Path.StartsWithSegments("/device-access") &&
              context.Request.Query.ContainsKey("logonToken"))
          {
            return LogonTokenAuthenticationSchemeOptions.DefaultScheme;
          }

          if (appOptions.EnableInteractiveBearerLogin &&
              context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
          {
            return IdentityConstants.BearerScheme;
          }

          // If the request has a Personal Access Token header, use PAT authentication
          if (context.Request.Headers.ContainsKey(PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName))
          {
            return PersonalAccessTokenAuthenticationSchemeOptions.DefaultScheme;
          }

          // If the request carries a service account api key, authenticate as a service account.
          if (context.Request.Headers.ContainsKey(ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName))
          {
            return ServiceAccountCredentialAuthenticationSchemeOptions.DefaultScheme;
          }

          // Device-signed agent requests are deliberately absent here. This scheme is reached only by
          // a policy that names it, so a device cannot authenticate to a bare [Authorize] endpoint
          // whose default policy asks only for an authenticated user.
          return IdentityConstants.ApplicationScheme;
        };
      });

    if (!string.IsNullOrWhiteSpace(appOptions.MicrosoftClientId) &&
        !string.IsNullOrWhiteSpace(appOptions.MicrosoftClientSecret))
    {
      authBuilder.AddMicrosoftAccount(microsoftOptions =>
      {
        microsoftOptions.ClientId = appOptions.MicrosoftClientId;
        microsoftOptions.ClientSecret = appOptions.MicrosoftClientSecret;
      });
    }

    if (!string.IsNullOrWhiteSpace(appOptions.GitHubClientId) &&
        !string.IsNullOrWhiteSpace(appOptions.GitHubClientSecret))
    {
      authBuilder.AddGitHub(options =>
      {
        options.ClientId = appOptions.GitHubClientId;
        options.ClientSecret = appOptions.GitHubClientSecret;
      });
    }

    // Add personal access token authentication.
    authBuilder.AddScheme<PersonalAccessTokenAuthenticationSchemeOptions, PersonalAccessTokenAuthenticationHandler>(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultScheme,
      _ => { });

    // Add logon token authentication.
    authBuilder.AddScheme<LogonTokenAuthenticationSchemeOptions, LogonTokenAuthenticationHandler>(
      LogonTokenAuthenticationSchemeOptions.DefaultScheme,
      _ => { });

    // Add service account credential authentication (x-api-key).
    authBuilder.AddScheme<ServiceAccountCredentialAuthenticationSchemeOptions, ServiceAccountCredentialAuthenticationHandler>(
      ServiceAccountCredentialAuthenticationSchemeOptions.DefaultScheme,
      _ => { });

    // Add installed-agent signature authentication (x-agent-signature).
    authBuilder.AddScheme<AgentSignatureAuthenticationSchemeOptions, AgentSignatureAuthenticationHandler>(
      AgentSignatureAuthenticationSchemeOptions.DefaultScheme,
      _ => { });

    hostBuilder.Services
      .AddScoped<PasskeySignInManager>()
      .AddScoped<IUserCreator, UserCreator>();
  }
}