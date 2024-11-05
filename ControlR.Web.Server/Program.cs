using System.Net;
using Bitbound.WebSocketBridge.Common.Extensions;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Web.Client.Extensions;
using ControlR.Web.Server.Authz;
using ControlR.Web.Server.Components;
using ControlR.Web.Server.Components.Account;
using ControlR.Web.Server.Hubs;
using ControlR.Web.Server.Middleware;
using ControlR.Web.ServiceDefaults;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;
using Npgsql;
using _Imports = ControlR.Web.Client._Imports;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

var builder = WebApplication.CreateBuilder(args);

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
    options.SignIn.RequireConfirmedEmail = appOptions.RequireUserEmailConfirmation;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
  })
  .AddRoles<IdentityRole<Guid>>()
  .AddEntityFrameworkStores<AppDb>()
  .AddSignInManager()
  .AddDefaultTokenProviders();

builder.Services.AddScoped<IUserCreator, UserCreator>();

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
await ConfigureForwardedHeaders();

// Add client services for pre-rendering.
builder.Services.AddControlrWebClient(string.Empty);

// Add HTTP clients.
builder.Services.AddHttpClient<IIpApi, IpApi>();
builder.Services.AddHttpClient<IControlrApi, ControlrApi>(ConfigureHttpClient);
builder.Services.AddHttpClient<IWsBridgeApi, WsBridgeApi>();

// Add other services.
builder.Services.AddSingleton<IEmailSender<AppUser>, IdentityEmailSender>();
builder.Services.AddLazyDi();
builder.Services.AddOutputCache();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ISystemTime, SystemTime>();
builder.Services.AddSingleton<IFileProvider>(new PhysicalFileProvider(builder.Environment.ContentRootPath));
builder.Services.AddSingleton<IMemoryProvider, MemoryProvider>();
builder.Services.AddSingleton<IRetryer, Retryer>();
builder.Services.AddSingleton<IDelayer, Delayer>();
builder.Services.AddSingleton<IServerStatsProvider, ServerStatsProvider>();
builder.Services.AddSingleton<IUserRegistrationProvider, UserRegistrationProvider>();
builder.Services.AddSingleton<IEmailSender, EmailSender>();
builder.Services.AddSingleton<IConnectionCounter, ConnectionCounter>();
builder.Services.AddWebSocketBridge();

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

app.UseWhen(
  ctx => ctx.Request.Method == HttpMethods.Head && ctx.Request.Path.StartsWithSegments("/downloads"),
  appBuilder => { appBuilder.UseMiddleware<ContentHashHeaderMiddleware>(); });

app.UseStaticFiles();

app.MapWebSocketBridge();
app.MapHub<AgentHub>("/hubs/agent");

app.UseAntiforgery();

app.MapControllers();

app.UseWhen(
  ctx => !ctx.Request.Path.StartsWithSegments("/api"),
  _ =>
  {
    app.MapRazorComponents<App>()
      .AddInteractiveWebAssemblyRenderMode()
      .AddAdditionalAssemblies(typeof(_Imports).Assembly);
  });

app.MapAdditionalIdentityEndpoints();

app.MapHub<ViewerHub>("/hubs/viewer");

app.UseOutputCache();

await app.ApplyMigrations();
await app.SetAllDevicesOffline();

await app.RunAsync();

return;

async Task ConfigureForwardedHeaders()
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

    string[] ipNetworks = [..ip4Networks, ..ip6Networks ];
    
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
}

void ConfigureHttpClient(IServiceProvider services, HttpClient client)
{
  var options = services.GetRequiredService<IOptionsMonitor<AppOptions>>();
  client.BaseAddress = options.CurrentValue.ServerBaseUri ??
                       throw new InvalidOperationException("ServerBaseUri cannot be empty.");
}