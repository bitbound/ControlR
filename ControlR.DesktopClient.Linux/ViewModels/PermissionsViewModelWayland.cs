using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Linux.Services;
using ControlR.DesktopClient.Views.Linux;
using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.ViewModels.Linux;

public interface IPermissionsViewModelWayland : IPermissionsViewModel
{
  IAsyncRelayCommand GrantRemoteControlPermissionCommand { get; }
  bool IsRemoteControlPermissionGranted { get; }
  Task SetPermissionValues();
}

public partial class PermissionsViewModelWayland(
  IDesktopClientPermissionService desktopClientPermissionService,
  ILogger<PermissionsViewModelWayland> logger,
  IWaylandPermissionProvider waylandPermissionProvider) : ViewModelBase<PermissionsViewWayland>, IPermissionsViewModelWayland
{
  private readonly IDesktopClientPermissionService _desktopClientPermissionService = desktopClientPermissionService;
  private readonly ILogger<PermissionsViewModelWayland> _logger = logger;
  private readonly IWaylandPermissionProvider _waylandPermissionProvider = waylandPermissionProvider;

  [ObservableProperty]
  private bool _isRemoteControlPermissionGranted;

  public async Task SetPermissionValues()
  {
    _logger.LogInformation("Refreshing Wayland permission view state.");
    var permissionState = await _desktopClientPermissionService.GetPermissionState(DesktopClientPermissionScope.RemoteControl).ConfigureAwait(false);
    var isGranted = permissionState.ArePermissionsGranted;
    IsRemoteControlPermissionGranted = isGranted;
    _logger.LogInformation("Wayland permission view state refreshed. Granted={Granted}", isGranted);
  }

  protected override async Task OnInitializeAsync()
  {
    await base.OnInitializeAsync();
    await SetPermissionValues();
  }

  [RelayCommand]
  private async Task GrantRemoteControlPermission()
  {
    _logger.LogInformation("Wayland Grant Permission command invoked.");
    var isGranted = await _waylandPermissionProvider.RequestRemoteControlPermission(bypassRestoreToken: true);
    _logger.LogInformation("Wayland Grant Permission command completed. Granted={Granted}", isGranted);
    await SetPermissionValues();
  }
}
