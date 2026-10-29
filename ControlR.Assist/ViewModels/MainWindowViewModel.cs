using ControlR.Libraries.Branding;

namespace ControlR.Assist.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
  [ObservableProperty]
  private ViewModelBase _currentViewModel;

  public MainWindowViewModel(HomeViewModel homeViewModel)
  {
    _currentViewModel = homeViewModel;
  }

  public string WindowTitle => $"{BrandingConstants.BrandName}  |  {Localization.AssistWindowTitle}";
}
