using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Views.Mac;
using ControlR.Libraries.Shared.Enums;

namespace ControlR.DesktopClient.ViewModels.Mac;

public interface IPermissionsViewModelMac : IPermissionsViewModel
{
  IAsyncRelayCommand GrantMacAccessibilityPermissionCommand { get; }
  IAsyncRelayCommand GrantMacScreenCapturePermissionCommand { get; }
  bool IsMacAccessibilityPermissionGranted { get; }
  bool IsMacScreenCapturePermissionGranted { get; }
  IRelayCommand OpenAccessibilitySettingsCommand { get; }
  IRelayCommand OpenScreenCaptureSettingsCommand { get; }
  string? PermissionStatusMessage { get; }
  MessageSeverity PermissionStatusSeverity { get; }
  Task SetPermissionValues();
}

[SuppressMessage("Performance", "CA1822:Mark members as static")]
public partial class PermissionsViewModelMac(IServiceProvider serviceProvider) : ViewModelBase<PermissionsViewMac>, IPermissionsViewModelMac
{
  private readonly IServiceProvider _serviceProvider = serviceProvider;

  [ObservableProperty]
  private bool _isMacAccessibilityPermissionGranted;

  [ObservableProperty]
  private bool _isMacScreenCapturePermissionGranted;

  [ObservableProperty]
  private string? _permissionStatusMessage;

  [ObservableProperty]
  private MessageSeverity _permissionStatusSeverity = MessageSeverity.Information;

  public async Task SetPermissionValues()
  {
    var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
    IsMacAccessibilityPermissionGranted = macInterop.IsMacAccessibilityPermissionGranted();
    IsMacScreenCapturePermissionGranted = macInterop.IsMacScreenCapturePermissionGranted();

    if (!IsMacAccessibilityPermissionGranted || !IsMacScreenCapturePermissionGranted)
    {
      PermissionStatusMessage = Localization.MacPermissionStaleHint;
      PermissionStatusSeverity = MessageSeverity.Information;
    }
    else
    {
      PermissionStatusMessage = null;
      PermissionStatusSeverity = MessageSeverity.Information;
    }

    await Task.CompletedTask;
  }

  protected override async Task OnInitializeAsync()
  {
    await base.OnInitializeAsync();
    await SetPermissionValues();
  }
  [RelayCommand]
  private async Task GrantMacAccessibilityPermission()
  {
    var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
    if (!macInterop.RequestAccessibilityPermission())
    {
      PermissionStatusMessage = Localization.MacAccessibilityPermissionRestartRequired;
      PermissionStatusSeverity = MessageSeverity.Warning;
    }
    await SetPermissionValues();
  }

  [RelayCommand]
  private async Task GrantMacScreenCapturePermission()
  {
    var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
    if (!macInterop.RequestScreenCapturePermission())
    {
      PermissionStatusMessage = Localization.MacScreenCapturePermissionRestartRequired;
      PermissionStatusSeverity = MessageSeverity.Warning;
    }
    await SetPermissionValues();
  }

  [RelayCommand]
  private void OpenAccessibilitySettings()
  {
    var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
    macInterop.OpenAccessibilityPreferences();
  }

  [RelayCommand]
  private void OpenScreenCaptureSettings()
  {
    var macInterop = _serviceProvider.GetRequiredService<IMacInterop>();
    macInterop.OpenScreenRecordingPreferences();
  }
}
