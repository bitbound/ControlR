using System.Collections.ObjectModel;

namespace ControlR.DesktopClient.ViewModels.Fakes;
internal class MainWindowViewModelFake : ViewModelBaseFake<MainWindow>, IMainWindowViewModel
{
  public MainWindowViewModelFake()
  {
    CurrentViewModel = new ConnectionsViewModelFake();
    NavigationItems.AddRange(
      new NavItemViewModelFake("home_regular", "Connections"),
      new NavItemViewModelFake("shield_keyhole_regular", "Permissions"),
      new NavItemViewModelFake("settings_regular", "Settings"),
      new NavItemViewModelFake("question_circle_regular", "About")
    );
  }

  public IViewModelBase? CurrentViewModel { get; set; }

  public ObservableCollection<INavItemViewModel> NavigationItems { get; } = [];

  public void HandleMainWindowClosed()
  {
    // Do nothing
  }
}