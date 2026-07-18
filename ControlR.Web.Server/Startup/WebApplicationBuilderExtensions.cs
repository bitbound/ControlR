using ControlR.Web.ServiceDefaults;
using ControlR.Libraries.DataRedaction;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz;
using ControlR.Web.Server.Data.Configuration;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;
using ControlR.Web.Server.Components.Account;
using ControlR.Libraries.WebSocketRelay.Common.Extensions;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Services.Tenants;
using ControlR.Web.Server.Services.LogonTokens;
using ControlR.Web.Client.Services;
using Microsoft.AspNetCore.Http.Features;
using ControlR.Web.Server.Services.AgentInstaller;
using ControlR.Web.Server.Services.DeviceManagement;
using Microsoft.AspNetCore.Authentication.BearerToken;
using System.Globalization;
using System.Threading.RateLimiting;
using ControlR.Libraries.Shared.Services.Encryption;
using ControlR.Web.Server.Services.Settings;
using ControlR.Web.Server.ExceptionHandlers;

namespace ControlR.Web.Server.Startup;

public static class WebApplicationBuilderExtensions
{
  public static async Task<IHostApplicationBuilder> AddControlrServer(
    this IHostApplicationBuilder builder,
    bool isOpenApiBuild)
  {
    if (isOpenApiBuild)
    {
      var inMemoryData = new Dictionary<string, string?>
      {
        { "AppOptions:UseInMemoryDatabase", "true" },
        { "AppOptions:InMemoryDatabaseName", "ControlR" }
      };
      builder.Configuration.AddInMemoryCollection(inMemoryData);
    }

    // Add environment variables ton configuration.
    builder.Configuration.AddEnvironmentVariables("ControlR_");

    // If enabled, add configuration provider for Docker secrets.
    var enableDockerSecrets = builder.Configuration
      .GetSection(AppOptions.SectionKey)
      .GetValue<bool>(nameof(AppOptions.EnableDockerSecrets));

    if (enableDockerSecrets)
    {

      if (OperatingSystem.IsWindows())
      {
        throw new PlatformNotSupportedException("Docker secrets configuration provider is not supported on Windows.");
      }

      builder.Configuration.AddKeyPerFile(
        directoryPath: "/run/secrets",
        optional: false,
        reloadOnChange: true);
    }

    builder.Services.Configure<AppOptions>(
      builder.Configuration.GetSection(AppOptions.SectionKey));

    builder.Services.Configure<AspireDashboardOptions>(
      builder.Configuration.GetSection(AspireDashboardOptions.SectionKey));

    builder.Services.Configure<ServerLifecycleOptions>(
      builder.Configuration.GetSection(ServerLifecycleOptions.SectionKey));

    builder.Services.Configure<BootstrapOptions>(
      builder.Configuration.GetSection(BootstrapOptions.SectionKey));

    var appOptions = builder.Configuration
      .GetSection(AppOptions.SectionKey)
      .Get<AppOptions>() ?? new AppOptions();

    // Configure logging.
    builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
    builder.Services.AddStarRedactor();

    if (!builder.Environment.IsEnvironment("Testing"))
    {
      // Add telemetry.
      builder.AddServiceDefaults(ServiceNames.Controlr, useServiceDiscovery: true);
    }
    else
    {
      builder.AddDefaultHealthChecks();
    }

    if (appOptions.UseInMemoryDatabase)
    {
      builder.Services.AddDbContextFactory<AppDb>((_, options) =>
      {
        var dbName = string.IsNullOrWhiteSpace(appOptions.InMemoryDatabaseName)
          ? Guid.NewGuid().ToString("N")
          : appOptions.InMemoryDatabaseName;

        options.UseInMemoryDatabase(dbName);
        options.EnableDetailedErrors(appOptions.EnableDatabaseDetailedErrors);
        options.AddInterceptors(new ServiceAccountInvariantInterceptor());
      }, lifetime: ServiceLifetime.Transient);
    }
    else
    {
      builder.AddControlrPostgresDb(appOptions);
    }

    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    // Add MudBlazor services
    builder.Services.AddMudServices();

    // Add components.
    builder.Services
      .AddRazorComponents()
      .AddInteractiveWebAssemblyComponents()
      .AddAuthenticationStateSerialization();

    // Add API services.
    builder.Services.Configure<FormOptions>(options =>
    {
      if (appOptions.MaxFileTransferSize <= 0)
      {
        options.MultipartBodyLengthLimit = long.MaxValue;
      }
      else
      {
        options.MultipartBodyLengthLimit = appOptions.MaxFileTransferSize;
      }
    });

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<ApiExceptionHandler>();
    builder.Services.AddExceptionHandler<UiExceptionHandler>();

    builder.AddControlrOpenApi();

    builder.Services.AddCors(options =>
    {
      if (appOptions.EnableCors && appOptions.CorsAllowedOrigins is { Length: > 0 } origins)
      {
        options.AddDefaultPolicy(policy =>
        {
          policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
      }
    });

    builder.Services.AddRateLimiter(options =>
    {
      options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

      options.OnRejected = (context, cancellationToken) =>
      {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfterValue)
          ? Math.Ceiling(retryAfterValue.TotalSeconds).ToString(CultureInfo.InvariantCulture)
          : null;

        if (!string.IsNullOrWhiteSpace(retryAfter))
        {
          context.HttpContext.Response.Headers.RetryAfter = retryAfter;
        }

        return ValueTask.CompletedTask;
      };

      options.AddPolicy(
        AnonymousAuthRateLimitPolicy.PolicyName,
        AnonymousAuthRateLimitPolicy.Create());
    });

    // Add authn/authz services.
    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddScoped<IdentityRedirectManager>();
    builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

    builder.Services.ConfigureApplicationCookie(options =>
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

    builder.Services
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

    builder.Services.AddScoped<IAuthorizationHandler, ServiceProviderRequirementHandler>();
    builder.Services.AddScoped<IAuthorizationHandler, ServiceProviderAsyncRequirementHandler>();
    builder.Services.AddScoped<IDeviceAccessScopeResolver, DeviceAccessScopeResolver>();

    // Add Identity services.
    builder.Services
      .AddIdentityApiEndpoints<AppUser>(options =>
      {
        options.User.RequireUniqueEmail = appOptions.RequireUserUniqueEmail;
        options.SignIn.RequireConfirmedEmail = appOptions.RequireUserEmailConfirmation;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
      })
      .AddRoles<AppRole>()
      .AddEntityFrameworkStores<AppDb>()
      .AddSignInManager()
      .AddDefaultTokenProviders();

    if (appOptions.EnableInteractiveBearerLogin)
    {
      builder.Services.Configure<BearerTokenOptions>(IdentityConstants.BearerScheme, options =>
      {
        options.BearerTokenExpiration = TimeSpan.FromMinutes(appOptions.InteractiveBearerTokenExpirationMinutes);
        options.RefreshTokenExpiration = TimeSpan.FromDays(appOptions.InteractiveRefreshTokenExpirationDays);
      });
    }

    var authBuilder = builder.Services
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

          // Otherwise, use Identity cookies for web UI
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

    builder.Services
      .AddScoped<PasskeySignInManager>()
      .AddScoped<IUserCreator, UserCreator>();

    // Configure DataProtection.
    builder.AddControlrDataProtection();

    // Add SignalR.
    builder.Services
      .AddSignalR(options =>
      {
        options.EnableDetailedErrors = appOptions.EnableSignalrDetailedErrors;
        options.MaximumReceiveMessageSize = 100_000;
        options.MaximumParallelInvocationsPerClient = 2;
      })
      .AddMessagePackProtocol()
      .AddJsonProtocol(options => { options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true; });

    // Add forwarded headers.
    await builder.AddControlrForwardedHeaders(appOptions);

    if (appOptions.UseHttpLogging)
    {
      builder.Services.AddHttpLogging(options =>
      {
        options.RequestHeaders.Add("X-Forwarded-For");
        options.RequestHeaders.Add("X-Forwarded-Proto");
        options.RequestHeaders.Add("X-Forwarded-Host");
        options.RequestHeaders.Add("X-Original-For");
        options.RequestHeaders.Add("X-Original-Proto");
        options.RequestHeaders.Add("X-Original-Host");
        options.RequestHeaders.Add("CF-Connecting-IP");
        options.RequestHeaders.Add("CF-RAY");
        options.RequestHeaders.Add("CF-IPCountry");
        options.RequestHeaders.Add("CDN-Loop");
        options.LoggingFields = HttpLoggingFields.All ^ HttpLoggingFields.RequestQuery;
      });
    }

    // Add other services.

    builder.Services.AddSingleton<IEmailSender<AppUser>, IdentityEmailSender>();

    builder.Services.AddOutputCache();
    builder.Services.AddMemoryCache();
    builder.Services.AddLazyInjection();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<IFileProvider>(new PhysicalFileProvider(builder.Environment.ContentRootPath));
    builder.Services.AddSingleton<IMemoryProvider, MemoryProvider>();
    builder.Services.AddSingleton<IRetryer, Retryer>();
    builder.Services.AddSingleton<IWaiter, Waiter>();
    builder.Services.AddSingleton<IServerStatsProvider, ServerStatsProvider>();
    builder.Services.AddSingleton<IPublicRegistrationBootstrapGate, PublicRegistrationBootstrapGate>();
    builder.Services.AddSingleton<EmailSender>();
    builder.Services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<EmailSender>());
    builder.Services.AddSingleton<IControlrEmailSender>(sp => sp.GetRequiredService<EmailSender>());
    builder.Services.AddSingleton<IHubStreamStore, HubStreamStore>();
    builder.Services.AddSingleton<IEd25519KeyProvider, Ed25519KeyProvider>();
    builder.Services.AddWebSocketRelay(options =>
    {
      options.RequireAuthenticationForRequester = true;
    });
    builder.Services.AddSingleton<ILogonTokenProvider, LogonTokenProvider>();
    builder.Services.AddSingleton<AgentInstallerKeyUsageCleanupBackgroundService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<AgentInstallerKeyUsageCleanupBackgroundService>());
    builder.Services.AddSingleton<ExternalUserCleanupBackgroundService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<ExternalUserCleanupBackgroundService>());
    builder.Services.AddScoped<IAgentInstallerKeyManager, AgentInstallerKeyManager>();
    builder.Services.AddScoped<IAgentVersionProvider, AgentVersionProvider>();
    builder.Services.AddScoped<IReleaseNotesProvider, ReleaseNotesProvider>();
    builder.Services.AddScoped<IPasswordManager, PasswordManager>();
    builder.Services.AddScoped<IPersonalAccessTokenManager, PersonalAccessTokenManager>();
    builder.Services.AddScoped<IPasswordHasher<string>, PasswordHasher<string>>();
    builder.Services.AddScoped<IDeviceManager, DeviceManager>();
    builder.Services.AddScoped<IEffectiveUserPreferencesResolver, EffectiveUserPreferencesResolver>();
    builder.Services.AddScoped<IUserPreferencesManager, UserPreferencesManager>();
    builder.Services.AddScoped<ITenantSettingsManager, TenantSettingsManager>();
    builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
    builder.Services.AddScoped<IUserPreferencesProvider>(services => services.GetRequiredService<IUserPreferencesManager>());
    builder.Services.AddScoped<IUserStorageManager, UserStorageManager>();
    builder.Services.AddScoped<IPublicServerSettingsProvider, PublicServerSettingsProviderServer>();
    builder.Services.AddScoped<ITenantInvitesProvider, TenantInvitesProvider>();
    builder.Services.AddScoped<IServiceAccountManager, ServiceAccountManager>();

    return builder;
  }
}