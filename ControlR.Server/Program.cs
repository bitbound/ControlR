using ControlR.Server.Auth;
using ControlR.Server.Hubs;
using ControlR.Server.Middleware;
using ControlR.Server.Options;
using ControlR.Server.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Serilog;
using System.Net;
using StackExchange.Redis;
using ControlR.Server.Services.Interfaces;
using ControlR.Server.Services.Distributed;
using ControlR.Server.Services.Distributed.Locking;
using ControlR.Server.Services.Local;
using ControlR.Libraries.Shared;
using ControlR.Libraries.Shared.Services.Http;
using ControlR.Libraries.Shared.Services.Buffers;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("ControlR_");

builder.Services.Configure<ApplicationOptions>(
    builder.Configuration.GetSection(ApplicationOptions.SectionKey));

var appOptions = builder.Configuration
    .GetSection(ApplicationOptions.SectionKey)
    .Get<ApplicationOptions>() ?? new();

ConfigureSerilog(appOptions);

builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = AuthSchemes.DigitalSignature;
    options.AddScheme(AuthSchemes.DigitalSignature, builder =>
    {
        builder.DisplayName = "Digital Signature";
        builder.HandlerType = typeof(DigitalSignatureAuthenticationHandler);
    });
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(PolicyNames.DigitalSignaturePolicy, builder =>
    {
        builder.AddAuthenticationSchemes(AuthSchemes.DigitalSignature);
        builder.RequireAssertion(x =>
        {
            return x.User?.Identity?.IsAuthenticated == true;
        });
        builder.RequireClaim(ClaimNames.PublicKey);
        builder.RequireClaim(ClaimNames.Username);
    })
    .AddPolicy(PolicyNames.RequireAdministratorPolicy, builder =>
    {
        builder.AddAuthenticationSchemes(AuthSchemes.DigitalSignature);
        builder.RequireAssertion(x =>
        {
            return x.User?.Identity?.IsAuthenticated == true;
        });
        builder.RequireClaim(ClaimNames.IsAdministrator, "true");
    });

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

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors();
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

if (appOptions.UseRedisBackplane)
{
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

builder.Services.AddOutputCache();

builder.Services.AddHttpClient<IMeteredApi, MeteredApi>();

builder.Services.AddSingleton<IKeyProvider, KeyProvider>();
builder.Services.AddSingleton<ISystemTime, SystemTime>();
builder.Services.AddSingleton<IFileProvider>(new PhysicalFileProvider(builder.Environment.ContentRootPath));
builder.Services.AddSingleton<IMemoryProvider, MemoryProvider>();
builder.Services.AddSingleton<IAppDataAccessor, AppDataAccessor>();
builder.Services.AddSingleton<IRetryer, Retryer>();
builder.Services.AddSingleton<IDelayer, Delayer>();
builder.Services.AddSingleton<IIceServerProvider, IceServerProvider>();
builder.Services.AddSingleton<IDigitalSignatureAuthenticator, DigitalSignatureAuthenticator>();
builder.Services.AddHttpContextAccessor();

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

app.UseCors(builder =>
{
    // This is Electron's origin.
    builder
        .WithOrigins("http://localhost:3000")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<Md5HeaderMiddleware>();

ConfigureStaticFiles(app);

app.UseAuthentication();
app.UseAuthorization();

app.UseOutputCache();

app.MapControllers();

app.MapHub<AgentHub>("/hubs/agent");
app.MapHub<ViewerHub>("/hubs/viewer");
app.MapHub<StreamerHub>("/hubs/streamer");
app.MapGet("/", x =>
{
    x.Response.Redirect("https://controlr.app");
    return Task.CompletedTask;
});

app.Run();

static void ConfigureStaticFiles(WebApplication app)
{
    app.UseStaticFiles();

    var provider = new FileExtensionContentTypeProvider();
    // Add new mappings
    provider.Mappings[".msix"] = "application/octet-stream";
    provider.Mappings[".apk"] = "application/octet-stream";
    provider.Mappings[".cer"] = "application/octet-stream";
    var downloadsPath = Path.Combine(app.Environment.WebRootPath, "downloads");
    Directory.CreateDirectory(downloadsPath);

    app.UseStaticFiles(new StaticFileOptions()
    {
        FileProvider = new PhysicalFileProvider(downloadsPath),
        ServeUnknownFileTypes = true,
        RequestPath = new PathString("/downloads"),
        ContentTypeProvider = provider,
        DefaultContentType = "application/octet-stream"
    });
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