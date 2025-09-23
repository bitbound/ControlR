
using CommunityToolkit.Mvvm.Input;

namespace ControlR.DesktopClient.ViewModels.Fakes;
internal class ManagedDeviceViewModelFake : ViewModelBaseFake, IManagedDeviceViewModel
{
  public string? AppVersion { get; set; } = "1.0.0";
  public bool IsAccessibilityPermissionGranted { get; set; } = false;

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

  public IRelayCommand OpenAccessibilitySettingsCommand { get; } = new RelayCommand(() => { });

  public IRelayCommand OpenScreenCaptureSettingsCommand { get; } = new RelayCommand(() => { });

  public void SetPermissionValues()
  {
    // No-op for the fake implementation
  }
}
