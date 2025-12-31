using ControlR.ApiClient;
using ControlR.ApiClientExample;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<ControlrApiClientOptions>(
    builder.Configuration.GetSection(ControlrApiClientOptions.SectionKey));

builder.AddControlrApiClient(ControlrApiClientOptions.SectionKey);

builder.Services.AddHostedService<ExampleService>();

var app = builder.Build();
await app.RunAsync();