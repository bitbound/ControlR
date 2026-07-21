using ControlR.Web.ServiceDefaults;
using ControlR.Libraries.DataRedaction;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Web.Server.Data.Configuration;
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
using System.Globalization;
using System.Threading.RateLimiting;
using ControlR.Libraries.Shared.Services.Encryption;
using ControlR.Web.Server.Services.Settings;
using ControlR.Web.Server.ExceptionHandlers;
using Microsoft.AspNetCore.SignalR;

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
      builder.AddServiceDefaults(
        ServiceNames.Controlr, 
        useServiceDiscovery: true,
        configureTracing: tracing =>
        {
          tracing
            .AddSource(DefaultActivitySources.Name)
            .AddSource(RemoteAccessSessionActivitySource.SourceName);
        });
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
    builder.AddControlrAuthorization();
    builder.AddControlrAuthentication(appOptions);

    // Configure DataProtection.
    builder.AddControlrDataProtection();

    // Add SignalR.
    builder.Services
      .AddSignalR(options =>
      {
        options.EnableDetailedErrors = appOptions.EnableSignalrDetailedErrors;
        options.MaximumReceiveMessageSize = 100_000;
        options.MaximumParallelInvocationsPerClient = 2;
        options.AddFilter<ViewerHubTraceFilter>();
      })
      .AddMessagePackProtocol()
      .AddJsonProtocol(options => { options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true; });

    // Add forwarded headers.
    await builder.AddControlrForwardedHeaders(appOptions);

    builder.AddControlrHttpLogging(appOptions);

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