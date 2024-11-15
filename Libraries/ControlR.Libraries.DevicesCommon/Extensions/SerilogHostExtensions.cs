using Microsoft.Extensions.Hosting;
using Serilog;

namespace ControlR.Libraries.DevicesCommon.Extensions;

public static class SerilogHostExtensions
{
  public static IHostApplicationBuilder BootstrapSerilog(
      this IHostApplicationBuilder hostBuilder,
      string logFilePath,
      TimeSpan logRetention,
      Action<LoggerConfiguration>? extraLoggerConfig = null)
  {
    try
    {
      void ApplySharedLoggerConfig(LoggerConfiguration loggerConfiguration)
      {
        loggerConfiguration
            .Destructure.ToMaximumDepth(3)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
            .WriteTo.File(logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileTimeLimit: logRetention,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}",
                shared: true);

        extraLoggerConfig?.Invoke(loggerConfiguration);
      }

      // https://github.com/serilog/serilog-aspnetcore#two-stage-initialization
      var loggerConfig = new LoggerConfiguration();
      ApplySharedLoggerConfig(loggerConfig);
      Log.Logger = loggerConfig.CreateBootstrapLogger();

      hostBuilder.Services.AddSerilog(
        (serviceProvider, configuration) =>
        {
          configuration
            .ReadFrom.Configuration(hostBuilder.Configuration)
            .ReadFrom.Services(serviceProvider);

          ApplySharedLoggerConfig(configuration);
        },
        preserveStaticLogger: true);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Failed to configure Serilog file logging.  Error: {ex.Message}");
    }
    return hostBuilder;
  }
}
