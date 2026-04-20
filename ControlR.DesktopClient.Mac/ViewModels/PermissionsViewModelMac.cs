using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Mac.Services;
using ControlR.DesktopClient.Views.Mac;
using ControlR.Libraries.Api.Contracts.Enums;

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
public partial class PermissionsViewModelMac(
  IMacInterop macInterop,
  IDesktopClientPermissionService desktopClientPermissionService) : ViewModelBase<PermissionsViewMac>, IPermissionsViewModelMac
{
  private readonly IDesktopClientPermissionService _desktopClientPermissionService = desktopClientPermissionService;
  private readonly IMacInterop _macInterop = macInterop;

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
    var platformState = await _desktopClientPermissionService.GetPlatformPermissionState();
    IsMacAccessibilityPermissionGranted = platformState.IsAccessibilityGranted == true;
    IsMacScreenCapturePermissionGranted = platformState.IsScreenCaptureGranted == true;

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
    if (!_macInterop.RequestAccessibilityPermission())
    {
      PermissionStatusMessage = Localization.MacAccessibilityPermissionRestartRequired;
      PermissionStatusSeverity = MessageSeverity.Warning;
    }

    await SetPermissionValues();
  }

  [RelayCommand]
  private async Task GrantMacScreenCapturePermission()
  {
    if (!_macInterop.RequestScreenCapturePermission())
    {
      PermissionStatusMessage = Localization.MacScreenCapturePermissionRestartRequired;
      PermissionStatusSeverity = MessageSeverity.Warning;
    }

    await SetPermissionValues();
  }

  [RelayCommand]
  private void OpenAccessibilitySettings()
  {
    _macInterop.OpenAccessibilityPreferences();
  }

  [RelayCommand]
  private void OpenScreenCaptureSettings()
  {
    _macInterop.OpenScreenRecordingPreferences();
  }
}
