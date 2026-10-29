using ControlR.Assist.Services;
using ControlR.Libraries.Branding;

namespace ControlR.Assist.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
  [ObservableProperty]
  private ViewModelBase _currentViewModel;

  public MainWindowViewModel(HomeViewModel homeViewModel, INavigationService navigationService)
  {
    _currentViewModel = homeViewModel;
    navigationService.NavigateRequested += viewModel => CurrentViewModel = viewModel;
  }

  public string WindowTitle => $"{BrandingConstants.BrandName}  |  {Localization.AssistWindowTitle}";
}
