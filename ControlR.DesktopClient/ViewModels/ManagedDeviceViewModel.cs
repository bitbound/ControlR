using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Services;

namespace ControlR.DesktopClient.ViewModels;

public interface IManagedDeviceViewModel : IViewModelBase
{
  string? AppVersion { get; }
  IAsyncRelayCommand GrantMacAccessibilityPermissionCommand { get; }
  IAsyncRelayCommand GrantMacScreenCapturePermissionCommand { get; }
  IAsyncRelayCommand GrantWaylandPermissionCommand { get; }
  bool IsLinuxWayland { get; }
  bool IsMacAccessibilityPermissionGranted { get; }
  bool IsMacOs { get; }
  bool IsMacScreenCapturePermissionGranted { get; }
  bool IsWaylandPermissionGranted { get; }
  IRelayCommand OpenAccessibilitySettingsCommand { get; }
  IRelayCommand OpenScreenCaptureSettingsCommand { get; }
  string? PermissionStatusMessage { get; }
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
  private bool _isMacAccessibilityPermissionGranted;
  [ObservableProperty]
  private bool _isMacScreenCapturePermissionGranted;
  [ObservableProperty]
  private bool _isWaylandPermissionGranted;
  [ObservableProperty]
  private string? _permissionStatusMessage;
  [ObservableProperty]
  private string _themeIconKey = "arrow_sync_circle_regular";
  [ObservableProperty]
  private string _themeModeText = Localization.ThemeAuto;

  public bool IsLinuxWayland { get; private set; }
  public bool IsMacOs { get; } = OperatingSystem.IsMacOS();

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
    if (OperatingSystem.IsMacOS())
    {
      var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
      IsMacAccessibilityPermissionGranted = macInterop.IsMacAccessibilityPermissionGranted();
      IsMacScreenCapturePermissionGranted = macInterop.IsMacScreenCapturePermissionGranted();

      if (!IsMacAccessibilityPermissionGranted || !IsMacScreenCapturePermissionGranted)
      {
        PermissionStatusMessage = Localization.MacPermissionStaleHint;
      }
      else
      {
        PermissionStatusMessage = null;
      }
    }
    else if (OperatingSystem.IsLinux())
    {
      var waylandPermissions = _serviceProvider.GetRequiredService<IWaylandPermissionProvider>();
      var isGranted = await waylandPermissions.IsRemoteControlPermissionGranted().ConfigureAwait(false);
      await Dispatcher.UIThread.InvokeAsync(() => IsWaylandPermissionGranted = isGranted);
    }
  }

  private void CheckPlatform()
  {
    if (OperatingSystem.IsLinux())
    {
      var detector = _serviceProvider.GetRequiredService<IDesktopEnvironmentDetector>();
      IsLinuxWayland = detector.IsWayland();
    }
  }
  [RelayCommand]
  private async Task GrantMacAccessibilityPermission()
  {
    if (OperatingSystem.IsMacOS())
    {
      var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
      macInterop.RequestAccessibilityPermission();
      await SetPermissionValues();
    }
  }

  [RelayCommand]
  private async Task GrantMacScreenCapturePermission()
  {
    if (OperatingSystem.IsMacOS())
    {
      var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
      macInterop.RequestScreenCapturePermission();
      await SetPermissionValues();
    }
  }

  [RelayCommand]
  private async Task GrantWaylandPermission()
  {
    if (OperatingSystem.IsLinux() && IsLinuxWayland)
    {
      var accessor = _serviceProvider.GetRequiredService<IWaylandPermissionProvider>();
      await accessor.RequestRemoteControlPermission(force: true);
      await SetPermissionValues();
    }
  }
  private void OnThemeChanged(object? sender, EventArgs e)
  {
    UpdateThemeModeText();
  }
  [RelayCommand]
  private void OpenAccessibilitySettings()
  {
    if (OperatingSystem.IsMacOS())
    {
      var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
      macInterop.OpenAccessibilityPreferences();
    }
  }

  [RelayCommand]
  private void OpenScreenCaptureSettings()
  {
    if (OperatingSystem.IsMacOS())
    {
      var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
      macInterop.OpenScreenRecordingPreferences();
    }
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