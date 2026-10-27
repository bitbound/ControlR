using Avalonia;

namespace ControlR.Assist;

internal sealed class Program
{
  // Avalonia configuration. Don't remove. Also used by the visual designer.
  public static AppBuilder BuildAvaloniaApp()
  {
    var appBuilder = AppBuilder
      .Configure<App>()
      .UsePlatformDetect();

#if IS_LINUX
    // Experimental as of Avalonia 12.1, so UsePlatformDetect() does not pick it up.
    // Falls back to X11 when no usable Wayland compositor is available.
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
    {
      appBuilder = appBuilder.UseWaylandWithFallback();
    }
#endif

    return appBuilder
      .WithInterFont()
#if DEBUG
      .WithDeveloperTools()
#endif
      .LogToTrace()
      .With(new MacOSPlatformOptions
      {
        ShowInDock = false
      });
  }

  [STAThread]
  public static int Main(string[] args) =>
    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
}
