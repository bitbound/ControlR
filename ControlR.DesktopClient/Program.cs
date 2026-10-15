using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ControlR.DesktopClient.Common.Startup;
using ControlR.DesktopClient.Services;
using ControlR.Libraries.Shared.Services.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.DesktopClient;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class Program
{
  private static AppBuilder? _appBuilder;
  private static IControlledApplicationLifetime? _lifetime;

  // Avalonia configuration, don't remove; also used by visual designer.
  // ReSharper disable once MemberCanBePrivate.Global
  public static AppBuilder BuildAvaloniaApp()
      => AppBuilder.Configure<App>()
          .UsePlatformDetect()
#if IS_LINUX
          // Experimental as of Avalonia 12.1, so UsePlatformDetect() does not pick it up.
          // Falls back to X11 when no usable Wayland compositor is available.
          .UseWaylandWithFallback()
#endif
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
          Console.WriteLine("Unexpected initialization state.");
        }
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("RenderTimer"))
      {
        Console.WriteLine(
          "An error occurred internally within Avalonia while activating the RenderTimer. " +
          "This can occur sometimes when the device is in a low-power mode. " +
          $"Error: {ex.Message}");

        Thread.Sleep(5_000);
        continue;
      }
      catch (Exception ex)
      {
        Console.WriteLine($"A fatal error occurred: {ex}");
        throw;
      }
      break;
    }
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

    Console.WriteLine("No X11 or Wayland display detected. Exiting.");
    Thread.Sleep(30_000);
    Environment.Exit(1);
  }

  /// <summary>
  ///   Applies the saved language before Avalonia loads any XAML, since localized text binds once at load.
  /// </summary>
  private static void UsePersistedCulture()
  {
    var instanceId = ArgsParser.GetArgValue<string?>("instance-id", defaultValue: string.Empty);
    var fileSystem = new FileSystem(NullLogger<FileSystem>.Instance);
    var settingsPath = StaticServiceProvider.GetDesktopSettingsPath(instanceId);
    var language = new DesktopSettingsStore(settingsPath, fileSystem).Current.Language;

    if (string.IsNullOrWhiteSpace(language))
    {
      return;
    }

    Localization.SetCulture(language);
  }
}
