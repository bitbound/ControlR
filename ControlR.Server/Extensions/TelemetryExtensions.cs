using OpenTelemetry.Resources;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry;
using OpenTelemetry.Exporter;

namespace ControlR.Server.Extensions;

public static class TelemetryExtensions
{
    private static readonly string _serviceName = "ControlR.Server";
    public static WebApplicationBuilder AddTelemetry(this WebApplicationBuilder builder)
    {
        var otlpEndpoint = builder.Configuration["OTLP_ENDPOINT_URL"];
        var azureConnectionString = builder.Configuration["AzureMonitor:ConnectionString"];

        builder.Logging
            .AddOpenTelemetry(options =>
            {
                var resources = ResourceBuilder
                    .CreateDefault()
                    .AddService(_serviceName);

                options.SetResourceBuilder(resources);
            });

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resBuilder =>
            {
                resBuilder.AddService(_serviceName);
            })
            .WithMetrics(meterProvider =>
            {
                meterProvider
                    .AddAspNetCoreInstrumentation()
                    .AddMeter("Microsoft.AspNetCore.Hosting")
                    .AddMeter("Microsoft.AspNetCore.Server.Kestrel");
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var otlpEndpointUri))
        {
            builder.Services
                .AddOpenTelemetry()
                .UseOtlpExporter(OtlpExportProtocol.Grpc, otlpEndpointUri);
        }
        if (!string.IsNullOrWhiteSpace(azureConnectionString))
        {
            // This will add exporters for logging, metrics, and tracing.
            builder.Services
                .AddOpenTelemetry()
                .UseAzureMonitor();
        }
        return builder;
    }
}
