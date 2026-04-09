namespace ControlR.Web.Client.Components.RemoteDisplays;

public partial class ExtrasPopover : DisposableComponent
{
  [Inject]
  public required ILogger<ExtrasPopover> Logger { get; init; }
  [Inject]
  public required IRemoteControlState RemoteControlState { get; init; }
  [Inject]
  public required IViewerRemoteControlStream RemoteControlStream { get; init; }
  [Inject]
  public required ISnackbar Snackbar { get; init; }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender)
    {
      Disposables.Add(RemoteControlState.OnStateChanged(() => InvokeAsync(StateHasChanged)));
    }

    await base.OnAfterRenderAsync(firstRender);
  }

  private void HandleMetricsToggled(bool value)
  {
    RemoteControlState.IsMetricsEnabled = value;
  }
}