using System.Collections.ObjectModel;
using ControlR.DesktopClient.Common.ViewModels;
using ControlR.DesktopClient.Services;

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
  IEnumerable<INavigationItemProvider> navigationItemProviders,
  INavigationProvider navigationProvider) : ViewModelBase<MainWindow>, IMainWindowViewModel
{
  private readonly IMainWindowProvider _mainWindowProvider = mainWindowProvider;
  private readonly IEnumerable<INavigationItemProvider> _navigationItemProviders = navigationItemProviders;
  private readonly INavigationProvider _navigationProvider = navigationProvider;
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

    var items = _navigationItemProviders
      .SelectMany(provider => provider.GetNavigationItems())
      .OrderBy(descriptor => descriptor.Order)
      .Select(descriptor =>
      {
        descriptor.ThrowIfInvalid();
        return _viewModelFactory.CreateNavItem(descriptor.ViewModelType, descriptor.IconKey, descriptor.Label);
      })
      .ToList();

    NavigationItems.AddRange(items);

    // Initialize nav items so they subscribe to navigation changes and set initial state
    var initTasks = items.Select(i => i.Initialize());
    await Task.WhenAll(initTasks);
  }
}
