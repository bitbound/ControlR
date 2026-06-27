using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ControlR.Libraries.Serilog;

public static class SerilogHostExtensions
{
  public static IHostApplicationBuilder BootstrapSerilog(
      this IHostApplicationBuilder hostBuilder,
      string logFilePath,
      TimeSpan logRetention,
      Action<LoggerConfiguration>? extraLoggerConfig = null)
  {
    var loggerConfig = new LoggerConfiguration();
    ApplySharedLoggerConfig(loggerConfig, logFilePath, logRetention, extraLoggerConfig);
    Log.Logger = loggerConfig.CreateBootstrapLogger();
    hostBuilder.Logging.AddSerilog(Log.Logger);

    return hostBuilder;
  }
  public static IServiceCollection BootstrapSerilog(
    this IServiceCollection services,
    IConfiguration configuration,
    string logFilePath,
    TimeSpan logRetention,
    Action<LoggerConfiguration>? extraLoggerConfig = null)
  {
    // https://github.com/serilog/serilog-aspnetcore#two-stage-initialization
    var loggerConfig = new LoggerConfiguration();
    ApplySharedLoggerConfig(loggerConfig, logFilePath, logRetention, extraLoggerConfig);
    Log.Logger = loggerConfig.CreateBootstrapLogger();

    services.AddSerilog(
      (serviceProvider, loggerConfig) =>
      {
        loggerConfig
          .ReadFrom.Configuration(configuration)
          .ReadFrom.Services(serviceProvider);

        ApplySharedLoggerConfig(loggerConfig, logFilePath, logRetention, extraLoggerConfig);
      },
      preserveStaticLogger: true);

    var logsDir = Path.GetDirectoryName(logFilePath);
    if (logsDir is not null)
    {
      _ = Directory.CreateDirectory(logsDir);
    }
    return services;
  }

  private static void ApplySharedLoggerConfig(
    LoggerConfiguration loggerConfiguration,
    string logFilePath,
    TimeSpan logRetention,
    Action<LoggerConfiguration>? extraLoggerConfig)
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
}
