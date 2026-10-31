using ControlR.Assist.Services;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Branding;
using CommunityToolkit.Mvvm.Input;

namespace ControlR.Assist.ViewModels;

public sealed partial class AboutViewModel(
  IUrlLauncher urlLauncher,
  IAssistSettingsService settingsService,
  INavigationService navigationService) : ViewModelBase
{
  private readonly INavigationService _navigationService = navigationService;
  private readonly IAssistSettingsService _settingsService = settingsService;
  private readonly IUrlLauncher _urlLauncher = urlLauncher;

  public string? AppVersion { get; } = typeof(AboutViewModel).Assembly.GetName().Version?.ToString(3);

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
