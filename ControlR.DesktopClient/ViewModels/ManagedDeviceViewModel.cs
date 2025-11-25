using System.Diagnostics.CodeAnalysis;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Services;

namespace ControlR.DesktopClient.ViewModels;

public interface IManagedDeviceViewModel : IViewModelBase
{
  string? AppVersion { get; }
  IAsyncRelayCommand GrantWaylandPermissionCommand { get; }
  bool IsAccessibilityPermissionGranted { get; }
  bool IsLinuxWayland { get; }
  bool IsMacOs { get; }
  bool IsScreenCapturePermissionGranted { get; }
  bool IsWaylandPermissionGranted { get; }
  IRelayCommand OpenAccessibilitySettingsCommand { get; }
  IRelayCommand OpenScreenCaptureSettingsCommand { get; }
  string ThemeIconKey { get; }
  string ThemeModeText { get; }
  IRelayCommand ToggleThemeCommand { get; }
  Task SetPermissionValues();
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
  private bool _isWaylandPermissionGranted;

  [ObservableProperty]
  private string _themeIconKey = "arrow_sync_circle_regular";

  [ObservableProperty]
  private string _themeModeText = Localization.ThemeAuto;

  public bool IsMacOs { get; } = OperatingSystem.IsMacOS();

  public bool IsLinuxWayland { get; private set; }

  public override async Task Initialize()
  {
    CheckPlatform();
    await SetPermissionValues();
    AppVersion = typeof(ManagedDeviceViewModel).Assembly.GetName().Version?.ToString();
    UpdateThemeModeText();
    _themeProvider.ThemeChanged += OnThemeChanged;
  }

  public async Task SetPermissionValues()
  {
#if MAC_BUILD
    var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
    IsAccessibilityPermissionGranted = macInterop.IsAccessibilityPermissionGranted();
    IsScreenCapturePermissionGranted = macInterop.IsScreenCapturePermissionGranted();
#elif LINUX_BUILD
    if (IsLinuxWayland)
    {
      var waylandPermissions = _serviceProvider.GetRequiredService<IWaylandPermissionProvider>();
      var isGranted = await waylandPermissions.IsRemoteControlPermissionGranted().ConfigureAwait(false);
      await Dispatcher.UIThread.InvokeAsync(() => IsWaylandPermissionGranted = isGranted);
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
#endif
  }

  [RelayCommand]
  private async Task GrantWaylandPermission()
  {
#if LINUX_BUILD
    if (IsLinuxWayland)
    {
      var accessor = _serviceProvider.GetRequiredService<IWaylandPermissionProvider>();
      await accessor.RequestRemoteControlPermission();
      await SetPermissionValues();
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