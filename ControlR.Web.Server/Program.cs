using System.Net;
using Bitbound.WebSocketBridge.Common.Extensions;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Web.Client.Authz.Policies;
using ControlR.Web.Client.Extensions;
using ControlR.Web.Server.Authz;
using ControlR.Web.Server.Components;
using ControlR.Web.Server.Components.Account;
using ControlR.Web.Server.Hubs;
using ControlR.Web.Server.Middleware;
using ControlR.Web.Server.Services.Distributed;
using ControlR.Web.Server.Services.Distributed.Locking;
using ControlR.Web.Server.Services.Local;
using ControlR.Web.ServiceDefaults;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;
using Npgsql;
using Serilog;
using StackExchange.Redis;
using _Imports = ControlR.Web.Client._Imports;

var builder = WebApplication.CreateBuilder(args);

// Configure IOptions.
builder.Configuration.AddEnvironmentVariables("ControlR_");

builder.Services.Configure<AppOptions>(
  builder.Configuration.GetSection(AppOptions.SectionKey));

var appOptions = builder.Configuration
  .GetSection(AppOptions.SectionKey)
  .Get<AppOptions>() ?? new AppOptions();

// Configure logging.
//ConfigureSerilog(appOptions);
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

// Add telemetry.
builder.AddServiceDefaults(ServiceNames.Controlr);

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

builder.Services.AddDbContext<AppDb>(options =>
  options.UseNpgsql(pgBuilder.ConnectionString));

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

builder.Services
  .AddAuthentication(options =>
  {
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
  })
  .AddIdentityCookies();

builder.Services
  .AddAuthorizationBuilder()
  .AddPolicy(RequireServerAdministratorPolicy.PolicyName, RequireServerAdministratorPolicy.Create())
  .AddPolicy(CanSelfRegisterPolicy.PolicyName, CanSelfRegisterPolicy.Create())
  .AddPolicy(DeviceAccessByDeviceResourcePolicy.PolicyName, DeviceAccessByDeviceResourcePolicy.Create());

builder.Services.AddScoped<IAuthorizationHandler, ServiceProviderRequirementHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ServiceProviderAsyncRequirementHandler>();

// Add Identity services.
builder.Services
  .AddIdentityCore<AppUser>(options =>
  {
    options.SignIn.RequireConfirmedEmail = appOptions.RequireUserEmailConfirmation;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
  })
  .AddRoles<IdentityRole<int>>()
  .AddEntityFrameworkStores<AppDb>()
  .AddSignInManager()
  .AddDefaultTokenProviders();

// Add SignalR.
var signalrBuilder = builder.Services
  .AddSignalR(options =>
  {
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 100_000;
  })
  .AddMessagePackProtocol()
  .AddJsonProtocol(options => { options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true; });

// Add forwarded headers.
ConfigureForwardedHeaders();

// Configure Redis, if scaled out.
await ConfigureRedis();

// Add client services for pre-rendering.
builder.Services.AddControlrWebClient(string.Empty);

// Add other services.
builder.Services.AddSingleton<IEmailSender<AppUser>, IdentityEmailSender>();
builder.Services.AddLazyDi();
builder.Services.AddOutputCache();

builder.Services.AddSingleton<ISystemTime, SystemTime>();
builder.Services.AddSingleton<IFileProvider>(new PhysicalFileProvider(builder.Environment.ContentRootPath));
builder.Services.AddSingleton<IMemoryProvider, MemoryProvider>();
builder.Services.AddSingleton<IAppDataAccessor, AppDataAccessor>();
builder.Services.AddSingleton<IRetryer, Retryer>();
builder.Services.AddSingleton<IDelayer, Delayer>();
builder.Services.AddSingleton<IServerStatsProvider, ServerStatsProvider>();
builder.Services.AddSingleton<IUserRegistrationProvider, UserRegistrationProvider>();
builder.Services.AddSingleton<IEmailSender, EmailSender>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<IIpApi, IpApi>();
builder.Services.AddHttpClient<IWsBridgeApi, WsBridgeApi>();
builder.Services.AddWebSocketBridge();

if (appOptions.UseRedisBackplane)
{
  builder.Services.AddSingleton<IDistributedLock, DistributedLock>();
  builder.Services.AddSingleton<IAlertStore, AlertStoreDistributed>();
  builder.Services.AddSingleton<IConnectionCounter, ConnectionCounterDistributed>();
  builder.Services.AddHostedService<ConnectionCountSynchronizer>();
}
else
{
  builder.Services.AddSingleton<IConnectionCounter, ConnectionCounterLocal>();
  builder.Services.AddSingleton<IAlertStore, AlertStoreLocal>();
}

builder.Host.UseSystemd();

var app = builder.Build();

app.UseForwardedHeaders();
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
  app.UseWebAssemblyDebugging();
  app.UseMigrationsEndPoint();
}
else
{
  app.UseHttpsRedirection();
  app.UseExceptionHandler("/Error", true);
  app.UseHsts();
}

app.UseMiddleware<ContentHashHeaderMiddleware>();

app.UseStaticFiles();

app.MapWebSocketBridge();
app.MapHub<AgentHub>("/hubs/agent");

app.UseAntiforgery();

app.MapControllers();

app.MapRazorComponents<App>()
  .AddInteractiveWebAssemblyRenderMode()
  .AddAdditionalAssemblies(typeof(_Imports).Assembly);

app.MapAdditionalIdentityEndpoints();

app.MapHub<ViewerHub>("/hubs/viewer");

app.UseOutputCache();

Log.Information("Applying migrations to database at host: {DbHost}", pgHost);
await app.ApplyMigrations();
await app.SetAllDevicesOffline();

await app.RunAsync();

return;

void ConfigureForwardedHeaders()
{
  builder.Services.Configure<ForwardedHeadersOptions>(options =>
  {
    options.ForwardedHeaders = ForwardedHeaders.All;
    options.ForwardLimit = null;

    // Default Docker host. We want to allow forwarded headers from this address.
    if (!string.IsNullOrWhiteSpace(appOptions?.DockerGatewayIp))
    {
      if (IPAddress.TryParse(appOptions?.DockerGatewayIp, out var dockerGatewayIp))
      {
        options.KnownProxies.Add(dockerGatewayIp);
      }
      else
      {
        Log.Error("Invalid DockerGatewayIp: {DockerGatewayIp}", appOptions?.DockerGatewayIp);
      }
    }

    if (appOptions?.KnownProxies is { Length: > 0 } knownProxies)
    {
      foreach (var proxy in knownProxies)
      {
        if (IPAddress.TryParse(proxy, out var ip))
        {
          options.KnownProxies.Add(ip);
        }
        else
        {
          Log.Error("Invalid KnownProxy IP: {KnownProxyIp}", proxy);
        }
      }
    }
  });
}

async Task ConfigureRedis()
{
  if (!appOptions.UseRedisBackplane)
  {
    return;
  }

  var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ??
                              throw new InvalidOperationException(
                                "Redis connection string cannot be empty if UseRedisBackplane is enabled.");

  signalrBuilder.AddStackExchangeRedis(redisConnectionString, options =>
  {
    options.Configuration.AbortOnConnectFail = false;
    options.Configuration.ChannelPrefix = RedisChannel.Literal("controlr-signalr");
  });

  builder.Services.AddStackExchangeRedisCache(options =>
  {
    options.Configuration = redisConnectionString;
    options.InstanceName = "controlr-cache";
  });

  var multiplexer =
    await ConnectionMultiplexer.ConnectAsync(redisConnectionString, options => { options.AllowAdmin = true; });

  if (!multiplexer.IsConnected)
  {
    Log.Fatal("Failed to connect to Redis backplane.");
  }

  builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
}

void ConfigureSerilog(AppOptions appOptions)
{
  var logsRetention = appOptions.LogRetentionDays;
  if (logsRetention <= 0)
  {
    logsRetention = 7;
  }

  var logsPath = Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory,
    "AppData",
    "logs");

  builder.BootstrapSerilog(Path.Combine(logsPath, "ControlR.Server.log"), TimeSpan.FromDays(logsRetention));
}