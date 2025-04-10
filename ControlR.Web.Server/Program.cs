using Bitbound.WebSocketBridge.Common.Extensions;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Web.Client.Extensions;
using ControlR.Web.Server.Authz;
using ControlR.Web.Server.Components;
using ControlR.Web.Server.Components.Account;
using ControlR.Web.Server.Data.Configuration;
using ControlR.Web.Server.Middleware;
using ControlR.Web.Server.Startup;
using ControlR.Web.ServiceDefaults;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;
using Npgsql;
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
builder.Services.AddHttpClient<IWsBridgeApi, WsBridgeApi>();

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
builder.Services.AddOutputCache();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IFileProvider>(new PhysicalFileProvider(builder.Environment.ContentRootPath));
builder.Services.AddSingleton<IMemoryProvider, MemoryProvider>();
builder.Services.AddSingleton<IRetryer, Retryer>();
builder.Services.AddSingleton<IDelayer, Delayer>();
builder.Services.AddSingleton<IServerStatsProvider, ServerStatsProvider>();
builder.Services.AddSingleton<IUserRegistrationProvider, UserRegistrationProvider>();
builder.Services.AddSingleton<IEmailSender, EmailSender>();
builder.Services.AddWebSocketBridge();
builder.Services.AddSingleton<IStreamStore, StreamStore>();
builder.Services.AddSingleton<IAgentInstallerKeyManager, AgentInstallerKeyManager>();
builder.Services.AddScoped<IDeviceManager, DeviceManager>();

builder.Host.UseSystemd();

var app = builder.Build();
app.UseForwardedHeaders();
if (appOptions.UseHttpLogging)
{
  app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/health"),
    appBuilder => appBuilder.UseHttpLogging());
}

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
  ctx => HttpMethods.IsHead(ctx.Request.Method) && ctx.Request.Path.StartsWithSegments("/downloads"),
  appBuilder => appBuilder.UseMiddleware<ContentHashHeaderMiddleware>());


app.MapStaticAssets();

app.MapWebSocketBridge();
app.MapHub<AgentHub>("/hubs/agent");

app.UseAuthentication();
app.UseAuthorization();
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

if (!app.Environment.IsDevelopment())
{
  app.UseOutputCache();
}

await app.ApplyMigrations();
await app.SetAllDevicesOffline();
await app.SetAllUsersOffline();

await app.RunAsync();