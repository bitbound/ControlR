using ControlR.Assist.ViewModels;
using ControlR.Assist.Views;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using Microsoft.Extensions.Logging;

namespace ControlR.Assist.Services;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddControlrAssist(this IServiceCollection services)
  {
    // Views and view models are registered with plain lambdas rather than the open-generic
    // overloads, which build an expression tree the linker has to guess at.
    services.AddSingleton<AboutViewModel>();
    services.AddSingleton<HomeViewModel>();
    services.AddSingleton<MainWindowViewModel>();
    services.AddSingleton<MainWindow>(provider => new MainWindow(provider.GetRequiredService<MainWindowViewModel>()));

    services.AddLogging();
    services.AddSingleton<IAssistSettingsService, AssistSettingsService>();
    services.AddSingleton<IAssistSessionHostFactory, AssistSessionHostFactory>();
    services.AddSingleton<IAssistSignalRClient, AssistHubClient>();
    services.AddSingleton<INavigationService, NavigationService>();
    services.AddSingleton<IUrlLauncher, UrlLauncher>();
    services.AddSingleton<AssistSessionController>(provider =>
    {
      var controller = new AssistSessionController(
        provider.GetRequiredService<IAssistSignalRClient>(),
        provider.GetRequiredService<IAssistSessionHostFactory>(),
        provider.GetRequiredService<IAssistSettingsService>(),
        provider.GetRequiredService<ILogger<AssistSessionController>>());

      var hubClient = provider.GetRequiredService<IAssistSignalRClient>();
      hubClient.OnConsentCancelled = controller.HandleConsentCancelled;
      hubClient.OnConsentRequested = controller.HandleConsentRequested;
      hubClient.OnConsentTimedOut = controller.HandleConsentTimedOut;
      hubClient.OnConnectionLost = controller.HandleConnectionLost;
      hubClient.OnSessionEnded = controller.HandleSessionEnded;
      hubClient.OnSessionStarted = controller.HandleSessionStarted;
      hubClient.OnReconnected = controller.HandleReconnected;

      return controller;
    });

    return services;
  }

#if DEBUG
  private sealed class DebugFileLoggerProvider(string path) : ILoggerProvider
  {
    private readonly Lock _lock = new();

    public ILogger CreateLogger(string categoryName) => new DebugFileLogger(categoryName, path, _lock);

    public void Dispose()
    {
    }

    private sealed class DebugFileLogger(string categoryName, string path, Lock log) : ILogger
    {
      public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

      public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

      public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
      {
        if (!IsEnabled(logLevel))
        {
          return;
        }

        var line = $"{DateTime.Now:HH:mm:ss.fff} [{logLevel}] {categoryName}: {formatter(state, exception)}" +
                   (exception is null ? string.Empty : $"\n  {exception}");

        lock (log)
        {
          File.AppendAllText(path, line + Environment.NewLine);
        }
      }
    }
  }
#endif
}
