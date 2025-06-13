using ControlR.Libraries.WebSocketRelay.Common.Extensions;
using ControlR.Web.Client.Components.Layout;
using ControlR.Web.Server.Components;
using ControlR.Web.Server.Components.Account;
using ControlR.Web.Server.Middleware;
using ControlR.Web.Server.Startup;
using ControlR.Web.ServiceDefaults;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

await builder.AddControlrServer();

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
app.UseStaticFiles(new StaticFileOptions
{
  FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.WebRootPath, "novnc")),
  RequestPath = "/novnc",
  ServeUnknownFileTypes = true,
});

app.MapWebSocketRelay();
app.MapHub<AgentHub>("/hubs/agent");

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
      .AddAdditionalAssemblies(typeof(MainLayout).Assembly);
  });

app.MapAdditionalIdentityEndpoints();

app.MapHub<ViewerHub>("/hubs/viewer");


if (appOptions.UseInMemoryDatabase)
{
  await app.AddBuiltInRoles();
}
else
{
  await app.ApplyMigrations();
  await app.SetAllDevicesOffline();
  await app.SetAllUsersOffline();
}

await app.RunAsync();