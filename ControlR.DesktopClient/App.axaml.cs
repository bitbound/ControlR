using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using ControlR.DesktopClient.Common.Startup;
using ControlR.DesktopClient.Services;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ControlR.DesktopClient;

public partial class App : Application
{
  private readonly CancellationTokenSource _appShutdownTokenSource = new();
  private readonly Lock _shutdownLock = new();
  private bool _hasShutdownStarted = false;

  public static Window MainWindow
  {
    get =>
      ServiceProvider.GetRequiredService<IMainWindowProvider>().MainWindow;
  }
  public static IServiceProvider ServiceProvider => StaticServiceProvider.Instance;

  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }

  public override void OnFrameworkInitializationCompleted()
  {
    var instanceId = ArgsParser.GetArgValue<string?>("instance-id", defaultValue: string.Empty);

    if (ApplicationLifetime is IControlledApplicationLifetime controlledLifetime)
    {
      // Listen for signals on POSIX systems to trigger a graceful shutdown.
      if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
      {
        _ = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
          context.Cancel = true;
          Dispatcher.UIThread.Invoke(() => controlledLifetime.Shutdown());
        });

        _ = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
        {
          context.Cancel = true;
          Dispatcher.UIThread.Invoke(() => controlledLifetime.Shutdown());
        });
      }

      StaticServiceProvider.Build(controlledLifetime, instanceId);

      controlledLifetime.Exit += (_, _) =>
      {
        HandleShutdownStarted().Forget();
      };

      // Set explicit shutdown for classic desktop style.
      if (controlledLifetime is ClassicDesktopStyleApplicationLifetime desktop)
      {
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        desktop.ShutdownRequested += (_, _) =>
        {
          HandleShutdownStarted().Forget();
        };
        if (SystemEnvironment.Instance.IsDebug)
        {
          desktop.MainWindow = StaticServiceProvider.Instance.GetRequiredService<MainWindow>();
        }
      }

      ReportAssemblyVersion();

      // Initialize theme from ThemeService
      var themeService = StaticServiceProvider.Instance.GetRequiredService<IThemeProvider>();
      RequestedThemeVariant = themeService.CurrentTheme;

      // Start the hosted services on a different thread.
      // This prevents hooks in the current UI thread from blocking
      // SetThreadDesktop when a remote control session starts.
      Task.Run(StartHostedServices)
        .GetAwaiter()
        .GetResult();

      // Set the DataContext for App.
      DataContext = StaticServiceProvider.Instance.GetRequiredService<IAppViewModel>();
    }
    else if (!Design.IsDesignMode)
    {
      throw new InvalidOperationException(
        "This application requires a classic desktop style lifetime.");
    }
    else
    {
      // Design-time services can be set up here if needed.
      RequestedThemeVariant = ThemeVariant.Dark;
    }

    base.OnFrameworkInitializationCompleted();
  }

  private static void ReportAssemblyVersion()
  {
    try
    {
      var logger = StaticServiceProvider.Instance.GetRequiredService<ILogger<App>>();
      var version = Assembly.GetExecutingAssembly().GetName().Version;
      logger.LogInformation("Desktop UI app initialized.  Assembly version: {AsmVersion}.", version);
    }
    catch (Exception ex)
    {
      Log.Warning(ex, "Error while reporting the assembly version.");
    }
  }

  private async Task HandleShutdownStarted()
  {
    if (!_shutdownLock.TryEnter())
    {
      return;
    }

    if (_hasShutdownStarted)
    {
      _shutdownLock.Exit();
      return;
    }

    _hasShutdownStarted = true;

    try
    {
      _appShutdownTokenSource.Cancel();
    }
    catch (Exception ex)
    {
      Log.Warning(ex, "Error while cancelling the app lifetime token.");
    }

    try
    {
      var hostedServices = StaticServiceProvider.Instance.GetServices<IHostedService>();
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      var stopTasks = hostedServices.Select(x => x.StopAsync(cts.Token));
      await Task.WhenAll(stopTasks).WaitAsync(cts.Token);
    }
    catch (Exception ex)
    {
      Log.Error(ex, "Error while stopping the hosted services during shutdown.");
      Environment.Exit(1);
    }
    finally
    {
      _shutdownLock.Exit();
    }
  }

  private async Task StartHostedServices()
  {
    var logger = StaticServiceProvider.Instance.GetRequiredService<ILogger<App>>();
    var hostedServices = StaticServiceProvider.Instance.GetServices<IHostedService>();
    foreach (var hostedService in hostedServices)
    {
      try
      {
        await hostedService.StartAsync(_appShutdownTokenSource.Token);
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error starting hosted service {HostedService}.", hostedService.GetType().Name);
      }
    }
  }
}