using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using ControlR.Web.Client.Extensions;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

if (builder.HostEnvironment.IsDevelopment())
{
  builder.Logging.SetMinimumLevel(LogLevel.Debug);
  builder.Logging.AddFilter("Microsoft.AspNetCore.Components.RenderTree.Renderer", LogLevel.Warning);
  builder.Logging.AddFilter("Microsoft.Extensions.Localization.ResourceManagerStringLocalizer", LogLevel.Warning);
}

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMudServices(config =>
{
  config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
});

builder.Services
  .AddAuthorizationCore(options =>
  {
    options.AddPolicy(RequireServerAdministratorPolicy.PolicyName, RequireServerAdministratorPolicy.Create());
  });

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();

builder.Services.AddControlrWebClient(builder.HostEnvironment.BaseAddress);

await builder.Build().RunAsync();
