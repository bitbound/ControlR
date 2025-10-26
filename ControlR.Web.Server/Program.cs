using ControlR.Libraries.WebSocketRelay.Common.Extensions;
using ControlR.Web.Client.Components.Layout;
using ControlR.Web.Server.Components;
using ControlR.Web.Server.Components.Account;
using ControlR.Web.Server.Middleware;
using ControlR.Web.Server.Startup;
using ControlR.Web.ServiceDefaults;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;
using System.Reflection;
using ControlR.Libraries.Shared.Constants;

var isOpenApiBuild = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSystemd();

await builder.AddControlrServer(isOpenApiBuild);

var appOptions = builder.Configuration
  .GetSection(AppOptions.SectionKey)
  .Get<AppOptions>() ?? new AppOptions();

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
  app.MapOpenApi();
  app.MapScalarApiReference();
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
app.UseStaticFiles(new StaticFileOptions
{
  FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "novnc")),
  RequestPath = "/novnc",
  ServeUnknownFileTypes = true,
});

app.MapWebSocketRelay();
app.MapHub<AgentHub>(AppConstants.AgentHubPath);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Configure output cache - must be before any middleware that generates response
app.UseOutputCache();

app.MapControllers();

app.UseWhen(
  ctx => !ctx.Request.Path.StartsWithSegments("/api"),
  _ =>
  {
    app.MapRazorComponents<App>()
      .AddInteractiveWebAssemblyRenderMode()
      .AddInteractiveServerRenderMode()
      .AddAdditionalAssemblies(typeof(MainLayout).Assembly);
  });

app.MapAdditionalIdentityEndpoints();

app.MapHub<MainBrowserHub>(AppConstants.MainBrowserHubPath);
app.MapHub<DeviceAccessHub>(AppConstants.DeviceAccessHubPath);

if (appOptions.UseInMemoryDatabase)
{
  await app.AddBuiltInRoles();
}
else
{
  await app.ApplyMigrations();
  await app.SetAllDevicesOffline();
  await app.SetAllUsersOffline();
  await app.RemoveEmptyTenants();
}

await app.RunAsync();