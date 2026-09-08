namespace ControlR.Web.Client.Components.Pages;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class Tags : ComponentBase
{
  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDeviceStore DeviceStore { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required ITagStore TagStore { get; init; }

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
    await DeviceStore.Refresh();
    await TagStore.Refresh();
  }
}
