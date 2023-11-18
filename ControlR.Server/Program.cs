using ControlR.Server.Auth;
using ControlR.Server.Data;
using ControlR.Server.Hubs;
using ControlR.Server.Middleware;
using ControlR.Server.Models;
using ControlR.Server.Services;
using ControlR.Shared;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Buffers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppOptions>(
    builder.Configuration.GetSection(nameof(AppOptions)));

builder.Services.AddDbContext<AppDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddRateLimiter(config =>
{
    config.AddFixedWindowLimiter(RateLimiterPolicies.CreateAccount, options =>
    {
        options.Window = TimeSpan.FromMinutes(1);
        options.PermitLimit = 6;
    });
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = AuthSchemes.DigitalSignature;
    options.AddScheme(AuthSchemes.DigitalSignature, builder =>
    {
        builder.DisplayName = "Digital Signature";
        builder.HandlerType = typeof(DigitalSignatureAuthenticationHandler);
    });
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PolicyNames.DigitalSignaturePolicy, builder =>
    {
        builder.AddAuthenticationSchemes(AuthSchemes.DigitalSignature);
        builder.RequireAssertion(x =>
        {
            return x.User?.Identity?.IsAuthenticated == true;
        });
        builder.RequireClaim(ClaimNames.PublicKey);
        builder.RequireClaim(ClaimNames.Username);
    });
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

builder.Services.AddSingleton<IKeyProvider, KeyProvider>();
builder.Services.AddSingleton<ISystemTime, SystemTime>();
builder.Services.AddSingleton<IProxyStreamStore, ProxyStreamStore>();
builder.Services.AddSingleton<IFileProvider>(new PhysicalFileProvider(builder.Environment.ContentRootPath));
builder.Services.AddSingleton<IMemoryProvider, MemoryProvider>();

builder.Host.UseSystemd();

var app = builder.Build();

app.UseForwardedHeaders();

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

ConfigureStaticFiles(app);

app.UseWhen(
    x => x.Request.Path.StartsWithSegments("/viewer-proxy"),
    builder =>
    {
        builder.UseWebSockets();
        builder.UseMiddleware<ViewerProxyMiddleware>();
    });

app.UseWhen(
    x => x.Request.Path.StartsWithSegments("/agent-proxy"),
    builder =>
    {
        builder.UseWebSockets();
        builder.UseMiddleware<AgentProxyMiddleware>();
    });

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<AgentHub>("/hubs/agent");
app.MapHub<ViewerHub>("/hubs/viewer");

var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
using (var scope = scopeFactory.CreateScope())
using (var appDb = scope.ServiceProvider.GetRequiredService<AppDb>())
{
    await appDb.Database.MigrateAsync();
}

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