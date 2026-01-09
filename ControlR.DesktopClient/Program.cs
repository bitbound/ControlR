using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.LinuxFramebuffer;
using ControlR.Libraries.Shared.Constants;

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
          .WithInterFont()
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
    while (true)
    {
      try
      {
        if (_appBuilder is null)
        {
          _appBuilder = BuildAvaloniaApp();

          if (Environment.GetEnvironmentVariable(AppConstants.WaylandLoginScreenVariable) is { } waylandLoginScreen &&
              bool.TryParse(waylandLoginScreen, out var isLoginScreen) &&
              isLoginScreen)
          {
            _appBuilder.StartLinuxFbDev(args);
          }
          else
          {
            _appBuilder.StartWithClassicDesktopLifetime(args, lifetime => { _lifetime = lifetime; });
          }
        }
        else if (_lifetime is ClassicDesktopStyleApplicationLifetime desktop)
        {
          desktop.Start(args);
        }
        else
        {
          Console.Error.Write("Unexpected initialization state.");
        }
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("RenderTimer"))
      {
        Console.Error.WriteLine(
          "An error occurred internally within Avalonia while activating the RenderTimer. " +
          "This can occur sometimes when the device is in a low-power mode. " +
          $"Error: {ex.Message}");

        Thread.Sleep(5_000);
        continue;
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"A fatal error occurred: {ex}");
        Thread.Sleep(5_000);
      }
      break;
    }
  }
}
