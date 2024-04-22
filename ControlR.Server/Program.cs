using ControlR.Server.Auth;
using ControlR.Server.Hubs;
using ControlR.Server.Middleware;
using ControlR.Server.Options;
using ControlR.Server.Services;
using ControlR.Shared;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Buffers;
using ControlR.Shared.Services.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Serilog;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("ControlR_");

builder.Services.Configure<ApplicationOptions>(
    builder.Configuration.GetSection(ApplicationOptions.SectionKey));

var appOptions = builder.Configuration
    .GetSection(ApplicationOptions.SectionKey)
    .Get<ApplicationOptions>() ?? new();

ConfigureSerilog(builder, appOptions);

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
    if (IPAddress.TryParse(appOptions?.DockerGatewayIp, out var dockerGatewayIp))
    {
        options.KnownProxies.Add(dockerGatewayIp);
    }
    else
    {
        Log.Error("Invalid DockerGatewayIp: {DockerGatewayIp}", appOptions?.DockerGatewayIp);
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
builder.Services
    .AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.MaximumReceiveMessageSize = 100_000;
        options.MaximumParallelInvocationsPerClient = 5;
    })
    .AddMessagePackProtocol();

builder.Services.AddHttpClient<IMeteredApi, MeteredApi>();

builder.Services.AddSingleton<IKeyProvider, KeyProvider>();
builder.Services.AddSingleton<ISystemTime, SystemTime>();
builder.Services.AddSingleton<IFileProvider>(new PhysicalFileProvider(builder.Environment.ContentRootPath));
builder.Services.AddSingleton<IMemoryProvider, MemoryProvider>();
builder.Services.AddSingleton<IConnectionCounter, ConnectionCounter>();
builder.Services.AddSingleton<IAppDataAccessor, AppDataAccessor>();
builder.Services.AddSingleton<IAlertStore, AlertStore>();
builder.Services.AddSingleton<IRetryer, Retryer>();
builder.Services.AddSingleton<IDelayer, Delayer>();
builder.Services.AddSingleton<IStreamerSessionCache, StreamerSessionCache>();
builder.Services.AddSingleton<IIceServerProvider, IceServerProvider>();

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

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<AgentHub>("/hubs/agent");
app.MapHub<ViewerHub>("/hubs/viewer");
app.MapHub<StreamerHub>("/hubs/streamer");

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

void ConfigureSerilog(WebApplicationBuilder webAppBuilder, ApplicationOptions applicationOptions)
{
    try
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

        void ApplySharedLoggerConfig(LoggerConfiguration loggerConfiguration)
        {
            loggerConfiguration
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
                .WriteTo.File($"{logsPath}/ControlR.Server.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileTimeLimit: TimeSpan.FromDays(logsRetention),
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}",
                    shared: true);
        }

        // https://github.com/serilog/serilog-aspnetcore#two-stage-initialization
        var loggerConfig = new LoggerConfiguration();
        ApplySharedLoggerConfig(loggerConfig);
        Log.Logger = loggerConfig.CreateBootstrapLogger();

        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services);

            ApplySharedLoggerConfig(configuration);
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to configure Serilog file logging.  Error: {ex.Message}");
    }
}