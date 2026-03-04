using System.Windows.Input;

namespace ControlR.DesktopClient.ViewModels.Fakes;
public class AppViewModelFake : IAppViewModel
{
  public IRelayCommand ExitApplicationCommand { get; } = new RelayCommand(() => { });
  public bool IsDarkMode { get; } = true;

  public IRelayCommand ShowMainWindowCommand { get; } = new RelayCommand(() => { });
  public IRelayCommand ToggleThemeCommand { get; } = new RelayCommand(() => { });
}
