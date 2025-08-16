using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ControlR.DesktopClient.ViewModels;

public interface IManagedDeviceViewModel : IViewModelBase
{
  bool IsAccessibilityPermissionGranted { get; }

  bool IsMacOs { get; }

  bool IsScreenCapturePermissionGranted { get; }

  IRelayCommand OpenAccessibilitySettingsCommand { get; }

  IRelayCommand OpenScreenCaptureSettingsCommand { get; }

  void SetPermissionValues();
}

public partial class ManagedDeviceViewModel(IServiceProvider serviceProvider) : ViewModelBase, IManagedDeviceViewModel
{
  private readonly IServiceProvider _serviceProvider = serviceProvider;

  [ObservableProperty]
  private bool _isAccessibilityPermissionGranted;

  [ObservableProperty]
  private bool _isScreenCapturePermissionGranted;

  public bool IsMacOs { get; } = OperatingSystem.IsMacOS();
  public override Task Initialize()
  {
    SetPermissionValues();
    return Task.CompletedTask;
  }

  public void SetPermissionValues()
  {
#if MAC_BUILD
    var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
    IsAccessibilityPermissionGranted = macInterop.IsAccessibilityPermissionGranted();
    IsScreenCapturePermissionGranted = macInterop.IsScreenCapturePermissionGranted();
#endif
  }

  [RelayCommand]
  private void OpenAccessibilitySettings()
  {
#if MAC_BUILD
    var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
    macInterop.OpenAccessibilityPreferences();
#endif
  }

  [RelayCommand]
  private void OpenScreenCaptureSettings()
  {
#if MAC_BUILD
    var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
    macInterop.OpenScreenRecordingPreferences();
#endif
  }
}
