
using CommunityToolkit.Mvvm.Input;
using ControlR.DesktopClient.Common;

namespace ControlR.DesktopClient.ViewModels.Fakes;
internal class ManagedDeviceViewModelFake : ViewModelBaseFake, IManagedDeviceViewModel
{
  public string? AppVersion { get; set; } = "1.0.0";
  public bool IsAccessibilityPermissionGranted { get; set; } = false;

  public bool IsLinuxWayland
  {
    get
    {
#if LINUX_BUILD
      return true;
#else
      return false;
#endif
    }
  }

  public bool IsMacOs
  {
    get
    {
#if MAC_BUILD
      return true;
#else
      return false;
#endif
    }
  }

  public bool IsScreenCapturePermissionGranted { get; set; } = true;
  public bool IsWaylandRemoteDesktopPermissionGranted { get; set; } = false;

  public IRelayCommand OpenAccessibilitySettingsCommand { get; } = new RelayCommand(() => { });

  public IRelayCommand OpenScreenCaptureSettingsCommand { get; } = new RelayCommand(() => { });
  public IRelayCommand OpenRemoteDesktopSettingsCommand { get; } = new RelayCommand(() => { });
  public IRelayCommand ToggleThemeCommand { get; } = new RelayCommand(() => { });

  public string ThemeModeText { get; set; } = Localization.ThemeAuto;
  public string ThemeIconKey { get; set; } = "arrow_sync_circle_regular";

  public void SetPermissionValues()
  {
    // No-op for the fake implementation
  }
}
