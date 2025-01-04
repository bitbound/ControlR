using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ControlR.Web.ServiceDefaults;

// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
  public static IHostApplicationBuilder AddServiceDefaults(
    this IHostApplicationBuilder builder,
    string serviceName)
  {
    builder.ConfigureOpenTelemetry(serviceName);

    builder.AddDefaultHealthChecks();

    builder.Services.AddServiceDiscovery();

    builder.Services.ConfigureHttpClientDefaults(http =>
    {
      // Turn on resilience by default
      http.AddStandardResilienceHandler();

      // Turn on service discovery by default
      http.AddServiceDiscovery();
    });

    return builder;
  }

  public static IHostApplicationBuilder ConfigureOpenTelemetry(
    this IHostApplicationBuilder builder,
    string serviceName)
  {
    builder.Logging.AddOpenTelemetry(logging =>
    {
      var resourceBuilder = ResourceBuilder.CreateDefault();
      resourceBuilder.AddService(serviceName, serviceNamespace: "controlr");

      logging.IncludeFormattedMessage = true;
      logging.IncludeScopes = true;
      logging.ParseStateValues = true;
    });

    builder.Services.AddOpenTelemetry()
      .ConfigureResource(resourceBuilder => { resourceBuilder.AddService(serviceName, serviceNamespace: "controlr"); })
      .WithMetrics(metrics =>
      {
        metrics
          .AddAspNetCoreInstrumentation()
          .AddHttpClientInstrumentation()
          .AddRuntimeInstrumentation();
      })
      .WithTracing(tracing =>
      {
        tracing
          .AddAspNetCoreInstrumentation(options =>
          {
            options.Filter = (httpContext) =>
            {
              return httpContext.Request.Path.Value?.StartsWith("/health") != true;
            };
          })
          .AddHttpClientInstrumentation(options =>
          {
            options.FilterHttpWebRequest = (request) =>
            {
              return !request.RequestUri.PathAndQuery.StartsWith("/health");
            };
          });
      });

    builder.AddOpenTelemetryExporters();

    return builder;
  }

  private static IHostApplicationBuilder AddOpenTelemetryExporters(
    this IHostApplicationBuilder builder)
  {
    var otlpEndpoint = builder.Configuration["OTLP_ENDPOINT_URL"];
    var azureMonitorConnectionString = builder.Configuration["AzureMonitor:ConnectionString"];

    if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var otlpUri))
    {
      builder.Services
        .AddOpenTelemetry()
        .UseOtlpExporter(OtlpExportProtocol.Grpc, otlpUri);
    }

    if (!string.IsNullOrWhiteSpace(azureMonitorConnectionString))
    {
      builder.Services
        .AddOpenTelemetry()
        .UseAzureMonitor(options => { options.ConnectionString = azureMonitorConnectionString; });
    }

    return builder;
  }

  public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
  {
    builder.Services
      .AddHealthChecks()
      // Add a default liveness check to ensure app is responsive
      .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

    return builder;
  }

  public static WebApplication MapDefaultEndpoints(this WebApplication app)
  {
    // All health checks must pass for app to be considered ready to accept traffic after starting
    app
      .MapHealthChecks("/health")
      .WithRequestTimeout(TimeSpan.FromSeconds(5))
      .CacheOutput(policy => { policy.Expire(TimeSpan.FromSeconds(5)); });

    // Only health checks tagged with the "live" tag must pass for app to be considered alive
    app
      .MapHealthChecks("/alive", new HealthCheckOptions
      {
        Predicate = r => r.Tags.Contains("live")
      })
      .WithRequestTimeout(TimeSpan.FromSeconds(5))
      .CacheOutput(policy => { policy.Expire(TimeSpan.FromSeconds(5)); });

    return app;
  }
}