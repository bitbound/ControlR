using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Services;
using ControlR.DesktopClient.ViewModels.Linux;
using ControlR.DesktopClient.ViewModels.Mac;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Services;

namespace ControlR.DesktopClient.ViewModels;

public interface IMainWindowViewModel : IViewModelBase
{
  IViewModelBase? CurrentViewModel { get; set; }
  ObservableCollection<INavItemViewModel> NavigationItems { get; }
  void HandleMainWindowClosed();
}

public partial class MainWindowViewModel(
  IMainWindowProvider mainWindowProvider,
  IViewModelFactory viewModelFactory,
  ISystemEnvironment systemEnvironment,
  INavigationProvider navigationProvider) : ViewModelBase<MainWindow>, IMainWindowViewModel
{
  private readonly IMainWindowProvider _mainWindowProvider = mainWindowProvider;
  private readonly INavigationProvider _navigationProvider = navigationProvider;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly IViewModelFactory _viewModelFactory = viewModelFactory;

  [ObservableProperty]
  private IViewModelBase? _currentViewModel;

  public ObservableCollection<INavItemViewModel> NavigationItems { get; } = [];

  public void HandleMainWindowClosed()
  {
    _mainWindowProvider.HandleMainWindowClosed();
  }

  protected override async Task OnInitializeAsync()
  {
    await base.OnInitializeAsync();
    await InitializeNavigationItemsAsync();
    await _navigationProvider.NavigateTo<IConnectionsViewModel>();
  }

  private async Task InitializeNavigationItemsAsync()
  {
    if (NavigationItems.Count > 0)
    {
      return;
    }

    var items = new List<INavItemViewModel>
    {
      _viewModelFactory.CreateNavItem<IConnectionsViewModel>("home_regular", Localization.Connections),
      _viewModelFactory.CreateNavItem<ISettingsViewModel>("settings_regular", Localization.Settings),
      _viewModelFactory.CreateNavItem<IAboutViewModel>("question_circle_regular", Localization.About)
    };

    switch (_systemEnvironment.Platform)
    {
      case SystemPlatform.MacOs:
        {
          items.Insert(1, _viewModelFactory.CreateNavItem<IPermissionsViewModelMac>("shield_keyhole_regular", Localization.Permissions));
          break;
        }
      case SystemPlatform.Linux:
        {
          items.Insert(1, _viewModelFactory.CreateNavItem<IPermissionsViewModelWayland>("shield_keyhole_regular", Localization.Permissions));
          break;
        }
      default:
        {
          items.Insert(1, _viewModelFactory.CreateNavItem<IPermissionsViewModel>("shield_keyhole_regular", Localization.Permissions));
          break;
        }
    }

    NavigationItems.AddRange(items);

    // Initialize nav items so they subscribe to navigation changes and set initial state
    var initTasks = items.Select(i => i.Initialize());
    await Task.WhenAll(initTasks);
  }
}
