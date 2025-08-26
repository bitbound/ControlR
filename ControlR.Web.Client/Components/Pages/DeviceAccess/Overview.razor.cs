using ControlR.Web.Client.Services.DeviceAccess;
using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public partial class Overview : IDisposable
{
  private readonly ConcurrentList<IDisposable> _disposables = [];

  [Inject]
  public required IDeviceAccessState DeviceAccessState { get; init; }

  public void Dispose()
  {
    Disposer.DisposeAll(_disposables);
    GC.SuppressFinalize(this);
  }

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
    _disposables.Add(DeviceAccessState.OnDeviceStateChanged(HandleDeviceStateChanged));
  }

  private async Task HandleDeviceStateChanged()
  {
    await InvokeAsync(StateHasChanged);
  }
}
