using System.Net;
using ControlR.Web.ServiceDefaults;
using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Web.Client.Extensions;
using ControlR.Web.Server.Authz;
using ControlR.Web.Server.Data.Configuration;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;
using ControlR.Web.Server.Components.Account;
using ControlR.Libraries.WebSocketRelay.Common.Extensions;
using ControlR.Web.Server.Middleware;

namespace ControlR.Web.Server.Startup;

public static class WebApplicationBuilderExtensions
{
  public static async Task<WebApplicationBuilder> AddControlrServer(this WebApplicationBuilder builder)
  {
    // Configure IOptions.
    builder.Configuration.AddEnvironmentVariables("ControlR_");

    builder.Services.Configure<AppOptions>(
      builder.Configuration.GetSection(AppOptions.SectionKey));

    var appOptions = builder.Configuration
      .GetSection(AppOptions.SectionKey)
      .Get<AppOptions>() ?? new AppOptions();

    // Configure logging.
    builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

    // Add telemetry.
    builder.AddServiceDefaults(ServiceNames.Controlr);

    if (appOptions.UseInMemoryDatabase)
    {
      builder.Services.AddDbContextFactory<AppDb>((sp, options) =>
      {
        var dbName = appOptions.InMemoryDatabaseName ?? "Controlr";
        options.UseInMemoryDatabase(dbName);
      }, lifetime: ServiceLifetime.Transient);
    }
    else
    {
      builder.AddPostgresDb();
    }

    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    // Add MudBlazor services
    builder.Services.AddMudServices();

    // Add components.
    builder.Services
      .AddRazorComponents()
      .AddInteractiveWebAssemblyComponents();

    // Add API services.
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddCors();

    // Add authn/authz services.
    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddScoped<IdentityUserAccessor>();
    builder.Services.AddScoped<IdentityRedirectManager>();
    builder.Services.AddScoped<AuthenticationStateProvider, PersistingRevalidatingAuthenticationStateProvider>();

    var authBuilder = builder.Services
      .AddAuthentication(options =>
      {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
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

    authBuilder.AddIdentityCookies();

    builder.Services
      .AddAuthorizationBuilder()
      .AddPolicy(RequireServerAdministratorPolicy.PolicyName, RequireServerAdministratorPolicy.Create())
      .AddPolicy(DeviceAccessByDeviceResourcePolicy.PolicyName, DeviceAccessByDeviceResourcePolicy.Create());

    builder.Services.AddScoped<IAuthorizationHandler, ServiceProviderRequirementHandler>();
    builder.Services.AddScoped<IAuthorizationHandler, ServiceProviderAsyncRequirementHandler>();

    // Add Identity services.
    builder.Services
      .AddIdentityCore<AppUser>(options =>
      {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = appOptions.RequireUserEmailConfirmation;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
      })
      .AddRoles<AppRole>()
      .AddEntityFrameworkStores<AppDb>()
      .AddSignInManager()
      .AddDefaultTokenProviders();

    builder.Services.AddScoped<IUserCreator, UserCreator>();

    // Configure DataProtection.
    builder.Services
      .AddDataProtection()
      .PersistKeysToDbContext<AppDb>();

    // Add SignalR.
    builder.Services
      .AddSignalR(options =>
      {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.MaximumReceiveMessageSize = 100_000;
      })
      .AddMessagePackProtocol()
      .AddJsonProtocol(options => { options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true; });

    // Add forwarded headers.
    await builder.ConfigureForwardedHeaders(appOptions);

    // Add client services for pre-rendering.
    builder.Services.AddControlrWebClient(string.Empty);

    // Add HTTP clients.
    builder.Services.AddHttpClient<IIpApi, IpApi>();
    builder.Services.AddHttpClient<IControlrApi, ControlrApi>(HttpClientConfigurer.ConfigureHttpClient);
    builder.Services.AddHttpClient<IWsRelayApi, WsRelayApi>();

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
        options.LoggingFields = HttpLoggingFields.All;
      });
    }

    // Add other services.

    builder.Services.AddSingleton<IEmailSender<AppUser>, IdentityEmailSender>();
    builder.Services.AddLazyDi();

    // Configure output cache
    builder.Services.AddOutputCache(options =>
    {
      // Define a named policy for device grid
      options.AddPolicy("DeviceGridPolicy", builder =>
      {
        builder
          .AddPolicy<DeviceGridOutputCachePolicy>()
          .Cache();
      });
    });

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<IFileProvider>(new PhysicalFileProvider(builder.Environment.ContentRootPath));
    builder.Services.AddSingleton<IMemoryProvider, MemoryProvider>();
    builder.Services.AddSingleton<IRetryer, Retryer>();
    builder.Services.AddSingleton<IDelayer, Delayer>();
    builder.Services.AddSingleton<IServerStatsProvider, ServerStatsProvider>();
    builder.Services.AddSingleton<IUserRegistrationProvider, UserRegistrationProvider>();
    builder.Services.AddSingleton<IEmailSender, EmailSender>();
    builder.Services.AddWebSocketRelay();
    builder.Services.AddSingleton<IStreamStore, StreamStore>();
    builder.Services.AddSingleton<IAgentInstallerKeyManager, AgentInstallerKeyManager>();
    builder.Services.AddScoped<IDeviceManager, DeviceManager>();

    builder.Host.UseSystemd();

    return builder;
  }
  public static async Task<WebApplicationBuilder> ConfigureForwardedHeaders(
    this WebApplicationBuilder builder,
    AppOptions appOptions)
  {
    var cloudflareIps = new List<IPNetwork>();

    if (appOptions.EnableCloudflareProxySupport)
    {
      using var httpClient = new HttpClient();
      using var ip4Response = await httpClient.GetAsync("https://www.cloudflare.com/ips-v4");
      ip4Response.EnsureSuccessStatusCode();
      var ip4Content = await ip4Response.Content.ReadAsStringAsync();
      var ip4Networks = ip4Content.Split();

      using var ip6Response = await httpClient.GetAsync("https://www.cloudflare.com/ips-v6");
      ip6Response.EnsureSuccessStatusCode();
      var ip6Content = await ip4Response.Content.ReadAsStringAsync();
      var ip6Networks = ip6Content.Split();

      string[] ipNetworks = [.. ip4Networks, .. ip6Networks];

      foreach (var network in ipNetworks)
      {
        if (!IPNetwork.TryParse(network, out var ipNetwork))
        {
          Console.WriteLine($"Invalid Cloudflare network: {network}");
        }
        else
        {
          Console.WriteLine($"Adding Cloudflare KnownNetwork: {network}");
          cloudflareIps.Add(ipNetwork);
        }
      }
    }

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
      options.ForwardedHeaders = ForwardedHeaders.All;
      options.ForwardLimit = null;

      // Default Docker host. We want to allow forwarded headers from this address.
      if (!string.IsNullOrWhiteSpace(appOptions.DockerGatewayIp))
      {
        if (IPAddress.TryParse(appOptions.DockerGatewayIp, out var dockerGatewayIp))
        {
          options.KnownProxies.Add(dockerGatewayIp);
        }
        else
        {
          Console.WriteLine($"Invalid DockerGatewayIp: {appOptions.DockerGatewayIp}");
        }
      }

      if (appOptions.KnownProxies is { Length: > 0 } knownProxies)
      {
        foreach (var proxy in knownProxies)
        {
          if (IPAddress.TryParse(proxy, out var ip))
          {
            Console.WriteLine($"Adding KnownProxy: {proxy}");
            options.KnownProxies.Add(ip);
          }
          else
          {
            Console.WriteLine("Invalid KnownProxy IP: {proxy}");
          }
        }
      }

      if (appOptions.KnownNetworks is { Length: > 0 } knownNetworks)
      {
        foreach (var network in knownNetworks)
        {
          if (IPNetwork.TryParse(network, out var ipNetwork))
          {
            Console.WriteLine($"Adding KnownNetwork: {network}");
            options.KnownNetworks.Add(ipNetwork);
          }
          else
          {
            Console.WriteLine("Invalid KnownNetwork: {network}");
          }
        }
      }

      if (cloudflareIps.Count > 0)
      {
        foreach (var cloudflareIp in cloudflareIps)
        {
          options.KnownNetworks.Add(cloudflareIp);
        }
      }
    });

    return builder;
  }

  private static WebApplicationBuilder AddPostgresDb(this WebApplicationBuilder builder)
  {

    // Add DB services.
    var pgUser = builder.Configuration.GetValue<string>("POSTGRES_USER");
    var pgPass = builder.Configuration.GetValue<string>("POSTGRES_PASSWORD");
    var pgHost = builder.Configuration.GetValue<string>("POSTGRES_HOST");

    ArgumentException.ThrowIfNullOrWhiteSpace(pgUser);
    ArgumentException.ThrowIfNullOrWhiteSpace(pgPass);
    ArgumentException.ThrowIfNullOrWhiteSpace(pgHost);

    if (Uri.TryCreate(pgHost, UriKind.Absolute, out var pgHostUri))
    {
      pgHost = pgHostUri.Authority;
    }

    var pgBuilder = new NpgsqlConnectionStringBuilder
    {
      Database = "controlr",
      Username = pgUser,
      Password = pgPass,
      Host = pgHost
    };

    builder.Services.AddDbContextFactory<AppDb>((sp, options) =>
    {
      options.UseNpgsql(pgBuilder.ConnectionString);
      var accessor = sp.GetRequiredService<IHttpContextAccessor>();
      if (accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user)
      {
        options.UseUserClaims(user);
        options.EnableDetailedErrors(builder.Environment.IsDevelopment());
      }
    }, lifetime: ServiceLifetime.Transient);

    return builder;
  }
}