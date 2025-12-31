using ControlR.ApiClient;
using ControlR.ApiClientExample;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<ControlrApiClientOptions>(
    builder.Configuration.GetSection(nameof(ControlrApiClientOptions)));

var clientOptions = builder.Configuration
  .GetSection(nameof(ControlrApiClientOptions))
  .Get<ControlrApiClientOptions>()
  ?? throw new InvalidOperationException("ControlrApiClientOptions must be configured.");

builder.AddControlrApiClient(nameof(ControlrApiClientOptions));

builder.Services.AddHostedService<ExampleService>();

var app = builder.Build();
await app.RunAsync();