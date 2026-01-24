
namespace ControlR.DesktopClient.ViewModels.Fakes;

internal class NavItemViewModelFake : ViewModelBase<NavItem>, INavItemViewModel
{
  public NavItemViewModelFake()
  {
  }

  public NavItemViewModelFake(string iconKey, string label)
  {
    IconKey = iconKey;
    Label = label;
  }
  public ThemeColor Color => IsSelected
    ? ThemeColor.Primary
    : ThemeColor.Default;

  public string IconKey { get; } = "home_regular";

  public bool IsSelected { get; set; }

  public string Label { get; } = "Connections";

  public IAsyncRelayCommand NavigateCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);
  public ControlThemeVariant Variant => IsSelected
    ? ControlThemeVariant.Outlined
    : ControlThemeVariant.Text;
}
