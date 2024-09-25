using ControlR.Web.Client;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using ControlR.Web.Client.Extensions;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMudServices();

builder.Services
  .AddAuthorizationCore(options =>
  {
    options.AddPolicy(PolicyNames.RequireAdministrator, AuthorizationPolicies.RequireAdministrator);
  });

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();

builder.Services.AddHttpClient<IVersionApi, VersionApi>(ConfigureHttpClient);

builder.Services.AddLazyDi();
builder.Services.AddControlrWebClient();

await builder.Build().RunAsync();

return;

void ConfigureHttpClient(IServiceProvider services, HttpClient client)
{
  client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
}