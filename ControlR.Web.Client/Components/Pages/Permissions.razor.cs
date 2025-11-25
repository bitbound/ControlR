using ControlR.Web.Client.StateManagement.Stores;
using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Components.Pages;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class Permissions : ComponentBase
{

  [Inject]
  public required IBusyCounter BusyCounter { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDeviceStore DeviceStore { get; init; }

  [Inject]
  public required IRoleStore RoleStore { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required IAdminTagStore TagStore { get; init; }

  [Inject]
  public required IUserStore UserStore { get; init; }

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
    await Refresh();
  }

  private async Task Refresh()
  {
    using var _ = BusyCounter.IncrementBusyCounter();
    await DeviceStore.Refresh();
    await TagStore.Refresh();
    await UserStore.Refresh();
    await RoleStore.Refresh();
  }
}