using Bitbound.WebSocketBridge.Common.Extensions;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Web.Server.Components;
using ControlR.Web.Server.Components.Account;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Services.Distributed.Locking;
using ControlR.Web.Server.Services.Distributed;
using ControlR.Web.Server.Services.Local;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;
using Serilog;
using StackExchange.Redis;
using System.Net;
using ControlR.Web.Server.Middleware;
using ControlR.Web.Server.Hubs;
using ControlR.Web.Client.Extensions;
using ControlR.Web.ServiceDefaults;
using ControlR.Web.Client.Auth;

var builder = WebApplication.CreateBuilder(args);

// Configure IOptions.
builder.Configuration.AddEnvironmentVariables("ControlR_");

builder.Services.Configure<ApplicationOptions>(
    builder.Configuration.GetSection(ApplicationOptions.SectionKey));

var appOptions = builder.Configuration
    .GetSection(ApplicationOptions.SectionKey)
    .Get<ApplicationOptions>() ?? new();

// Configure logging.
ConfigureSerilog(appOptions);
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

// Add telemetry.
builder.AddServiceDefaults();

// Add DB services.
var connectionString = builder.Configuration.GetConnectionString(ServiceNames.Postgres)
    ?? throw new InvalidOperationException("Connection string 'Postgres' not found.");

builder.Services.AddDbContext<AppDb>(options =>
    options.UseNpgsql(connectionString));

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
  .AddPolicy(PolicyNames.RequireAdministrator, policyBuilder =>
  {
    policyBuilder.RequireAuthenticatedUser();
    policyBuilder.RequireClaim(ClaimNames.IsAdministrator, "true");
  });

// Add Identity services.
builder.Services
  .AddIdentityCore<AppUser>(options => options.SignIn.RequireConfirmedAccount = true)
  .AddEntityFrameworkStores<AppDb>()
  .AddSignInManager()
  .AddDefaultTokenProviders();

// Add SignalR.
var signalrBuilder = builder.Services
  .AddSignalR(options =>
  {
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 100_000;
    options.MaximumParallelInvocationsPerClient = 5;
  })
  .AddMessagePackProtocol()
  .AddJsonProtocol(options =>
  {
    options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
  });

// Add forwarded headers.
ConfigureForwardedHeaders();

// Configure Redis, if scaled out.
await ConfigureRedis();

// Add other services.
builder.Services.AddSingleton<IEmailSender<AppUser>, IdentityNoOpEmailSender>();
builder.Services.AddLazyDi();
builder.Services.AddOutputCache();

builder.Services.AddSingleton<ISystemTime, SystemTime>();
builder.Services.AddSingleton<IFileProvider>(new PhysicalFileProvider(builder.Environment.ContentRootPath));
builder.Services.AddSingleton<IMemoryProvider, MemoryProvider>();
builder.Services.AddSingleton<IAppDataAccessor, AppDataAccessor>();
builder.Services.AddSingleton<IRetryer, Retryer>();
builder.Services.AddSingleton<IDelayer, Delayer>();
builder.Services.AddSingleton<IServerStatsProvider, ServerStatsProvider>();
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
  app.UseExceptionHandler("/Error", createScopeForErrors: true);
  app.UseHsts();
}

app.UseMiddleware<ContentHashHeaderMiddleware>();

app.UseStaticFiles();

app.MapWebSocketBridge("/bridge");
app.MapHub<AgentHub>("/hubs/agent");

app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(ControlR.Web.Client._Imports).Assembly);

app.MapAdditionalIdentityEndpoints();

app.MapControllers();

app.MapHub<ViewerHub>("/hubs/viewer");

app.UseOutputCache();

await app.ApplyMigrations<AppDb>();

app.Run();

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
            throw new InvalidOperationException("Redis connection string cannot be empty if UseRedisBackplane is enabled.");

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

  var multiplexer = await ConnectionMultiplexer.ConnectAsync(redisConnectionString, options =>
  {
    options.AllowAdmin = true;
  });

  if (!multiplexer.IsConnected)
  {
    Log.Fatal("Failed to connect to Redis backplane.");
  }

  builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
}

void ConfigureSerilog(ApplicationOptions applicationOptions)
{
  var logsRetention = applicationOptions.LogRetentionDays;
  if (logsRetention <= 0)
  {
    logsRetention = 7;
  }

  var logsPath = Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory,
    "AppData",
    "logs");

  builder.Host.BootstrapSerilog(Path.Combine(logsPath, "ControlR.Server.log"), TimeSpan.FromDays(logsRetention));
}