using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using ControlR.DesktopClient.Common.Startup;
using ControlR.DesktopClient.ViewModels;
using ControlR.DesktopClient.Views;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient;

public partial class App : Application
{
  private readonly CancellationTokenSource _appShutdownTokenSource = new();
  public static Window MainWindow
  {
    get =>
      ServiceProvider.GetRequiredService<IClassicDesktopStyleApplicationLifetime>().MainWindow ??=
      ServiceProvider.GetRequiredService<MainWindow>();
  }
  public static IServiceProvider ServiceProvider => StaticServiceProvider.Instance;
  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }
  
  public override void OnFrameworkInitializationCompleted()
  {
    var instanceId = ArgsParser.GetArgValue<string?>("instance-id", defaultValue: string.Empty);

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
      desktop.ShutdownRequested += (sender, args) =>
      {
        _appShutdownTokenSource.Cancel();
      };

      if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
      {
        _ = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
          context.Cancel = true;
          Avalonia.Threading.Dispatcher.UIThread.Invoke(() => desktop.Shutdown());
        });

        _ = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
        {
          context.Cancel = true;
          Avalonia.Threading.Dispatcher.UIThread.Invoke(() => desktop.Shutdown());
        });
      }

      StaticServiceProvider.Build(desktop, instanceId);

      ReportAssemblyVersion();

      // Start the hosted services on a different thread.
      // This prevents hooks in the current UI thread from blocking
      // SetThreadDesktop when a remote control session starts.
      Task.Run(StartHostedServices)
        .GetAwaiter()
        .GetResult();

      // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
      // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
      DisableAvaloniaDataAnnotationValidation();
      DataContext = StaticServiceProvider.Instance.GetRequiredService<IAppViewModel>();
    }
    else if (!Design.IsDesignMode)
    {
      throw new InvalidOperationException(
        "This application requires a classic desktop style lifetime.");
    }

    base.OnFrameworkInitializationCompleted();
  }

  private static void DisableAvaloniaDataAnnotationValidation()
  {
    // Get an array of plugins to remove
    var dataValidationPluginsToRemove =
        BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

    // remove each entry found
    foreach (var plugin in dataValidationPluginsToRemove)
    {
      BindingPlugins.DataValidators.Remove(plugin);
    }
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
      System.Diagnostics.Debug.WriteLine($"Error reporting assembly version: {ex.Message}");
    }
  }

  private async Task StartHostedServices()
  {
    var hostedServices = StaticServiceProvider.Instance.GetServices<IHostedService>();
    foreach (var hostedService in hostedServices)
    {
      try
      {
        await hostedService.StartAsync(_appShutdownTokenSource.Token);
      }
      catch (Exception ex)
      {
        System.Diagnostics.Debug.WriteLine($"Error starting hosted service {hostedService.GetType().Name}: {ex.Message}");
      }
    }
  }
}