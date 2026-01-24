using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.DesktopClient.Services;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.DesktopClient.ViewModels;

public interface INavItemViewModel : IViewModelBase
{
  public ThemeColor Color { get; }
  public string IconKey { get; }
  public bool IsSelected { get; set; }
  public string Label { get; }
  public IAsyncRelayCommand NavigateCommand { get; }
  public ControlThemeVariant Variant { get; }
}

public partial class NavItemViewModel<TDestination>(
  string iconKey,
  string label,
  INavigationProvider navigationProvider) : ViewModelBase<NavItem>, INavItemViewModel
  where TDestination : IViewModelBase
{
  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(Color), nameof(Variant))]
  private bool _isSelected;

  public ThemeColor Color => IsSelected
    ? ThemeColor.Primary
    : ThemeColor.Default;
  public string IconKey { get; } = iconKey;
  public string Label { get; } = label;
  public ControlThemeVariant Variant => IsSelected
    ? ControlThemeVariant.Outlined
    : ControlThemeVariant.Text;

  protected override Task OnInitializeAsync()
  {
    // Subscribe to navigation changes so only the active nav item is selected
    navigationProvider.NavigationOccurred += OnActiveViewModelTypeChanged;

    Disposables.Add(
      new CallbackDisposable(() => navigationProvider.NavigationOccurred -= OnActiveViewModelTypeChanged));

    // Set initial selection state
    OnActiveViewModelTypeChanged(navigationProvider.ActiveViewModel);

    return Task.CompletedTask;
  }

  [RelayCommand]
  private async Task NavigateAsync()
  {
    await navigationProvider.NavigateTo<TDestination>();
  }

  private void OnActiveViewModelTypeChanged(Type? type)
  {
    IsSelected = type == typeof(TDestination);
  }
}