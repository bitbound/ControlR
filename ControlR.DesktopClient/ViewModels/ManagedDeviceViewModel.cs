using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ControlR.DesktopClient.ViewModels;

public interface IManagedDeviceViewModel : IViewModelBase
{
  string? AppVersion { get; }
  bool IsAccessibilityPermissionGranted { get; }

  bool IsMacOs { get; }

  bool IsScreenCapturePermissionGranted { get; }

  IRelayCommand OpenAccessibilitySettingsCommand { get; }

  IRelayCommand OpenScreenCaptureSettingsCommand { get; }

  void SetPermissionValues();
}

public partial class ManagedDeviceViewModel(IServiceProvider serviceProvider) : ViewModelBase, IManagedDeviceViewModel
{
  #pragma warning disable IDE0052 // Remove unread private members
  // Required on Mac.
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  #pragma warning restore IDE0052 // Remove unread private members

  [ObservableProperty]
  private string? _appVersion;

  [ObservableProperty]
  private bool _isAccessibilityPermissionGranted;

  [ObservableProperty]
  private bool _isScreenCapturePermissionGranted;

  public bool IsMacOs { get; } = OperatingSystem.IsMacOS();
  public override Task Initialize()
  {
    SetPermissionValues();
    AppVersion = typeof(ManagedDeviceViewModel).Assembly.GetName().Version?.ToString();
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
