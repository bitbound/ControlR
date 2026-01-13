
using CommunityToolkit.Mvvm.Input;
using ControlR.DesktopClient.Common;

namespace ControlR.DesktopClient.ViewModels.Fakes;
internal class ManagedDeviceViewModelFake : ViewModelBaseFake, IManagedDeviceViewModel
{
  public string? AppVersion { get; set; } = "1.0.0";
  public IAsyncRelayCommand GrantMacAccessibilityPermissionCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);
  public IAsyncRelayCommand GrantMacScreenCapturePermissionCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);
  public IAsyncRelayCommand GrantWaylandPermissionCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);
  public bool IsLinuxWayland => OperatingSystem.IsLinux();
  public bool IsMacAccessibilityPermissionGranted { get; set; } = false;
  public bool IsMacOs => OperatingSystem.IsMacOS();
  public bool IsMacScreenCapturePermissionGranted { get; set; } = true;
  public bool IsWaylandPermissionGranted { get; set; } = false;
  public IRelayCommand OpenAccessibilitySettingsCommand { get; } = new RelayCommand(() => { });
  public IRelayCommand OpenScreenCaptureSettingsCommand { get; } = new RelayCommand(() => { });
  public string? PermissionStatusMessage { get; set; }
  public string ThemeIconKey { get; set; } = "arrow_sync_circle_regular";
  public string ThemeModeText { get; set; } = Localization.ThemeAuto;
  public IRelayCommand ToggleThemeCommand { get; } = new RelayCommand(() => { });

  public Task SetPermissionValues()
  {
    // No-op for the fake implementation
    return Task.CompletedTask;
  }
}
