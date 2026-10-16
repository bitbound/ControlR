using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ControlR.DesktopClient.Common.Startup;
using ControlR.DesktopClient.Services;
using ControlR.Libraries.Branding;
using Serilog;

namespace ControlR.DesktopClient;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class Program
{
  private static AppBuilder? _appBuilder;
  private static IControlledApplicationLifetime? _lifetime;

  // Avalonia configuration. Don't remove. Also used by visual designer.
  // The Wayland backend is deliberately not enabled. xdg-shell gives a client no way to place its
  // own toplevel, so windows that position themselves (e.g. custom toast notification) would stay
  // center-screen on Wayland. Once the self-positioning toast is replaced by a persistent
  // connection window, no window needs a chosen screen corner, and `UseWaylandWithFallback` can
  // be re-enabled.
  public static AppBuilder BuildAvaloniaApp()
      => AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .WithInterFont()
#if DEBUG
          .WithDeveloperTools()
#endif
          .LogToTrace()
          .With(new MacOSPlatformOptions()
          {
            ShowInDock = false
          });

  // Initialization code. Don't use any Avalonia, third-party APIs or any
  // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
  // yet and stuff might break.
  [STAThread]
  public static void Main(string[] args)
  {
    UseBootstrapLogger();
    UsePersistedCulture();
    UseLinuxDisplayGuard();

    while (true)
    {
      try
      {
        if (_appBuilder is null)
        {
          // AppBuilder has static internal state that will throw if it's configured more than once.
          _appBuilder = BuildAvaloniaApp();
          _appBuilder.StartWithClassicDesktopLifetime(args, lifetime => { _lifetime = lifetime; });
        }
        else if (_lifetime is ClassicDesktopStyleApplicationLifetime desktop)
        {
          desktop.Start(args);
        }
        else
        {
          Log.Error("Unexpected initialization state.");
        }
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("RenderTimer"))
      {
        Log.Warning(ex, "Avalonia failed to activate the RenderTimer, which can happen while the device is in a low-power mode. Retrying in 5 seconds.");

        Thread.Sleep(5_000);
        continue;
      }
      catch (Exception ex)
      {
        Log.Fatal(ex, "A fatal error occurred.");
        throw;
      }
      break;
    }
  }

  /// <summary>
  ///   Points Serilog at a brand-named file under the temp directory until the real log path is
  ///   known. The real path depends on the session username, so it cannot be resolved before the
  ///   username lookup whose failures this logger records.
  /// </summary>
  private static void UseBootstrapLogger()
  {
    Log.Logger = new LoggerConfiguration()
      .WriteTo.File(
        Path.Combine(Path.GetTempPath(), BrandingConstants.BrandKey, "Bootstrap.log"),
        rollingInterval: RollingInterval.Day,
        shared: true)
      .CreateBootstrapLogger();
  }

  /// <summary>
  ///   On Linux, the desktop client requires either an X11 or Wayland display to be available.
  ///   This method checks for the presence of a display and exits the application if none is found.
  ///   The delay prevents the agent from restarting the desktop client in a tight loop when no display is available.
  /// </summary>
  private static void UseLinuxDisplayGuard()
  {
    if (!OperatingSystem.IsLinux())
    {
      return;
    }

    var x11Display = Environment.GetEnvironmentVariable("DISPLAY");
    var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");

    if (!string.IsNullOrWhiteSpace(x11Display) || !string.IsNullOrWhiteSpace(waylandDisplay))
    {
      return;
    }

    Log.Warning("No X11 or Wayland display detected. Exiting.");
    Thread.Sleep(30_000);
    Environment.Exit(1);
  }

  /// <summary>
  ///   Applies the saved language before Avalonia loads any XAML, since localized text binds once at load.
  /// </summary>
  private static void UsePersistedCulture()
  {
    try
    {
      var instanceId = ArgsParser.GetArgValue<string?>("instance-id", defaultValue: string.Empty);
      using var provider = StaticServiceProvider.CreateTemporaryProvider(instanceId);
      var language = provider.GetRequiredService<IDesktopSettingsStore>().Current.Language;

      if (string.IsNullOrWhiteSpace(language))
      {
        return;
      }

      Localization.SetCulture(language);
    }
    catch (Exception ex)
    {
      // Runs before Avalonia and before the real log path is known. Starting in the system language
      // is better than not starting, and the store reads the same file again once the real log exists.
      Log.Warning(ex, "Unable to apply the saved language.");
    }
  }
}
