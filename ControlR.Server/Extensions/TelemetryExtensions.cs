using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

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

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    options
                        .AddOtlpExporter(otlpOptions =>
                        {
                            otlpOptions.Endpoint = new Uri(otlpEndpoint);
                        });
                }

                if (!string.IsNullOrWhiteSpace(azureConnectionString))
                {
                    options
                        .AddAzureMonitorLogExporter(azureMonitorOptions =>
                        {
                            azureMonitorOptions.ConnectionString = azureConnectionString;
                        });
                }
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
                    .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                    .AddPrometheusExporter();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    meterProvider
                        .AddOtlpExporter(otlpOptions =>
                        {
                            otlpOptions.Endpoint = new Uri(otlpEndpoint);
                        });
                }

                if (!string.IsNullOrWhiteSpace(azureConnectionString))
                {
                    meterProvider
                        .AddAzureMonitorMetricExporter(azureMonitorOptions =>
                        {
                            azureMonitorOptions.ConnectionString = azureConnectionString;
                        });
                }
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing
                        .AddOtlpExporter(otlpOptions =>
                        {
                            otlpOptions.Endpoint = new Uri(otlpEndpoint);
                        });
                }

                if (!string.IsNullOrWhiteSpace(azureConnectionString))
                {
                    tracing
                        .AddAzureMonitorTraceExporter(azureMonitorOptions =>
                        {
                            azureMonitorOptions.ConnectionString = azureConnectionString;
                        });
                }
            });

        return builder;
    }
}
