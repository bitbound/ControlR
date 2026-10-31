using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ControlR.Assist.Services;
using ControlR.Assist.Views;

namespace ControlR.Assist;

public partial class App : Application
{
  public static IServiceProvider ServiceProvider => StaticServiceProvider.Instance;

  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }

  public override void OnFrameworkInitializationCompleted()
  {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      StaticServiceProvider.Build();

      // Applied before the window is created so the first paint already uses the saved theme.
      RequestedThemeVariant = ServiceProvider.GetRequiredService<IThemeProvider>().CurrentTheme;

      desktop.MainWindow = ServiceProvider.GetRequiredService<MainWindow>();
      desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
    }
    else if (!Design.IsDesignMode)
    {
      throw new InvalidOperationException(
        "This application requires a classic desktop style lifetime.");
    }

    base.OnFrameworkInitializationCompleted();
  }
}
