using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ControlR.Web.Client.Startup;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

if (builder.HostEnvironment.IsDevelopment())
{
  builder.Logging.SetMinimumLevel(LogLevel.Debug);
  builder.Logging.AddFilter("Microsoft.AspNetCore.Components.RenderTree.Renderer", LogLevel.Warning);
  builder.Logging.AddFilter("Microsoft.Extensions.Localization.ResourceManagerStringLocalizer", LogLevel.Warning);
}

builder.Services
  .AddAuthorizationCore(options =>
  {
    options.AddPolicy(RequireServerAdministratorPolicy.PolicyName, RequireServerAdministratorPolicy.Create());
  });

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();
builder.Services.AddSingleton<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();

var baseUrl = new Uri(builder.HostEnvironment.BaseAddress);
builder.Services.AddControlrWebClient(baseUrl);

await builder.Build().RunAsync();
