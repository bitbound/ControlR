using ControlR.Assist.Services;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Branding;
using CommunityToolkit.Mvvm.Input;

namespace ControlR.Assist.ViewModels;

public sealed partial class AboutViewModel : ViewModelBase
{
  private readonly INavigationService _navigationService;
  private readonly IAssistSettingsService _settingsService;
  private readonly IUrlLauncher _urlLauncher;

  public AboutViewModel(IUrlLauncher urlLauncher, IAssistSettingsService settingsService, INavigationService navigationService)
  {
    _urlLauncher = urlLauncher;
    _settingsService = settingsService;
    _navigationService = navigationService;
    AppVersion = typeof(AboutViewModel).Assembly.GetName().Version?.ToString(3);
  }

  public string? AppVersion { get; }

  public bool HideSponsorshipInfo => BrandingConstants.HideSponsorshipInfo;

  public string? ServerUrl =>
    BrandingConstants.ControlrServerUrl?.ToString() ?? _settingsService.GetSettings().ServerUrl;

  [RelayCommand]
  private void Back() => _navigationService.NavigateTo<HomeViewModel>();

  [RelayCommand]
  private void OpenUrl(string? url)
  {
    if (!string.IsNullOrWhiteSpace(url))
    {
      _ = _urlLauncher.Open(url);
    }
  }
}
