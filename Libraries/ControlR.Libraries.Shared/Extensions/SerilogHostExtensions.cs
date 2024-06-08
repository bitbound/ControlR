using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ControlR.Libraries.Shared.Extensions;

public static class SerilogHostExtensions
{
    public static IHostBuilder BootstrapSerilog(
        this IHostBuilder hostBuilder,
        string logFilePath,
        TimeSpan logRetention)
    {
        try
        {
            void ApplySharedLoggerConfig(LoggerConfiguration loggerConfiguration)
            {
                loggerConfiguration
                    .Destructure.ToMaximumDepth(3)
                    .Enrich.FromLogContext()
                    .Enrich.WithThreadId()
                    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
                    .WriteTo.File(logFilePath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileTimeLimit: logRetention,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}",
                        shared: true);
            }

            // https://github.com/serilog/serilog-aspnetcore#two-stage-initialization
            var loggerConfig = new LoggerConfiguration();
            ApplySharedLoggerConfig(loggerConfig);
            Log.Logger = loggerConfig.CreateBootstrapLogger();

            hostBuilder.UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services);

                ApplySharedLoggerConfig(configuration);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to configure Serilog file logging.  Error: {ex.Message}");
        }
        return hostBuilder;
    }
}
