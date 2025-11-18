using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Services;

namespace ControlR.DesktopClient.ViewModels;

public interface IManagedDeviceViewModel : IViewModelBase
{
  string? AppVersion { get; }
  bool IsAccessibilityPermissionGranted { get; }
  bool IsLinuxWayland { get; }
  bool IsMacOs { get; }
  bool IsScreenCapturePermissionGranted { get; }
  bool IsWaylandRemoteDesktopPermissionGranted { get; }
  IRelayCommand OpenAccessibilitySettingsCommand { get; }
  IRelayCommand OpenRemoteDesktopSettingsCommand { get; }
  IRelayCommand OpenScreenCaptureSettingsCommand { get; }
  string ThemeIconKey { get; }
  string ThemeModeText { get; }
  IRelayCommand ToggleThemeCommand { get; }
  void SetPermissionValues();
}

[SuppressMessage("Performance", "CA1822:Mark members as static")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
public partial class ManagedDeviceViewModel(
  IServiceProvider serviceProvider,
  IThemeProvider themeProvider) : ViewModelBase, IManagedDeviceViewModel
{
  // Required on Mac.
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  private readonly IThemeProvider _themeProvider = themeProvider;

  [ObservableProperty]
  private string? _appVersion;

  [ObservableProperty]
  private bool _isAccessibilityPermissionGranted;

  [ObservableProperty]
  private bool _isScreenCapturePermissionGranted;

  [ObservableProperty]
  private bool _isWaylandRemoteDesktopPermissionGranted;

  [ObservableProperty]
  private string _themeIconKey = "arrow_sync_circle_regular";

  [ObservableProperty]
  private string _themeModeText = Localization.ThemeAuto;

  public bool IsMacOs { get; } = OperatingSystem.IsMacOS();

  public bool IsLinuxWayland { get; private set; }

  public override Task Initialize()
  {
    CheckPlatform();
    SetPermissionValues();
    AppVersion = typeof(ManagedDeviceViewModel).Assembly.GetName().Version?.ToString();
    UpdateThemeModeText();
    _themeProvider.ThemeChanged += OnThemeChanged;
    return Task.CompletedTask;
  }

  public void SetPermissionValues()
  {
#if MAC_BUILD
    var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
    IsAccessibilityPermissionGranted = macInterop.IsAccessibilityPermissionGranted();
    IsScreenCapturePermissionGranted = macInterop.IsScreenCapturePermissionGranted();
#elif LINUX_BUILD
    if (IsLinuxWayland)
    {
      var waylandInterop = _serviceProvider.GetRequiredService<IWaylandInterop>();
      IsScreenCapturePermissionGranted = waylandInterop.IsScreenCastPermissionGranted();
      IsWaylandRemoteDesktopPermissionGranted = waylandInterop.IsRemoteDesktopPermissionGranted();
    }
#endif
  }

  private void CheckPlatform()
  {
#if LINUX_BUILD
    var detector = _serviceProvider.GetRequiredService<IDesktopEnvironmentDetector>();
    IsLinuxWayland = detector.IsWayland();
#endif
  }

  private void OnThemeChanged(object? sender, EventArgs e)
  {
    UpdateThemeModeText();
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
#elif LINUX_BUILD
    if (IsLinuxWayland)
    {
      var waylandInterop = _serviceProvider.GetRequiredService<IWaylandInterop>();
      waylandInterop?.OpenWaylandPermissionsInfo();
    }
#endif
  }

  [RelayCommand]
  private void OpenRemoteDesktopSettings()
  {
#if LINUX_BUILD
    if (IsLinuxWayland)
    {
      var waylandInterop = _serviceProvider.GetRequiredService<IWaylandInterop>();
      waylandInterop?.OpenWaylandPermissionsInfo();
    }
#endif
  }

  [RelayCommand]
  private void ToggleTheme()
  {
    _themeProvider.ToggleTheme();
  }

  private void UpdateThemeModeText()
  {
    (ThemeModeText, ThemeIconKey) = _themeProvider.CurrentThemeMode switch
    {
      ThemeMode.Light => (Localization.ThemeLight, "weather_sunny_regular"),
      ThemeMode.Dark => (Localization.ThemeDark, "weather_moon_regular"),
      _ => (Localization.ThemeAuto, "arrow_sync_circle_regular")
    };
  }
}