using Avalonia;

namespace ControlR.DesktopClient;
internal sealed class Program
{
  private static AppBuilder? _appBuilder;

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
        _appBuilder ??= BuildAvaloniaApp();
        _appBuilder.StartWithClassicDesktopLifetime(args);
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
      break;
    }
  }

  // Avalonia configuration, don't remove; also used by visual designer.
  public static AppBuilder BuildAvaloniaApp()
      => AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .WithInterFont()
          .LogToTrace();
}
