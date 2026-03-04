using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace ControlR.DesktopClient.Services;

public interface IMainWindowProvider
{
  Window MainWindow { get; }
  void HandleMainWindowClosed();
}

public class MainWindowProvider(
  IControlledApplicationLifetime appLifetime,
  IServiceProvider serviceProvider) : IMainWindowProvider
{
  private readonly IControlledApplicationLifetime _appLifetime = appLifetime;
  private readonly Lock _mainWindowLock = new();
  private readonly IServiceProvider _serviceProvider = serviceProvider;

  private MainWindow? _controlledMainWindow;

  public Window MainWindow
  {
    get
    {
      using var _ = _mainWindowLock.EnterScope();
      
      if (_appLifetime is ClassicDesktopStyleApplicationLifetime desktop)
      {
        desktop.MainWindow ??= _serviceProvider.GetRequiredService<MainWindow>();
        return desktop.MainWindow;
      }

      _controlledMainWindow ??= _serviceProvider.GetRequiredService<MainWindow>();
      return _controlledMainWindow;
    }
  }

  public void HandleMainWindowClosed()
  {
    using var _ = _mainWindowLock.EnterScope();
    
    if (_appLifetime is ClassicDesktopStyleApplicationLifetime desktop)
    {
      desktop.MainWindow = null;
      return;
    }

    _controlledMainWindow = null;
  }
}