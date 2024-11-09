using ControlR.Web.Client.Services.Stores;
using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Components.Pages;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class Permissions : ComponentBase
{
  private readonly ConcurrentList<TagResponseDto> _tags = [];

  [Inject]
  public required IBusyCounter BusyCounter { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDeviceStore DeviceStore { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required ITagStore TagStore { get; init; }

  [Inject]
  public required IUserStore UserStore { get; init; }

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
    await Refresh();
    
    var tagResult = await ControlrApi.GetAllTags(true);
    if (!tagResult.IsSuccess)
    {
      Snackbar.Add("Failed to load tags", Severity.Error);
      return;
    }
    _tags.AddRange(tagResult.Value);
  }

  private async Task Refresh()
  {
    using var _ = BusyCounter.IncrementBusyCounter();
    await DeviceStore.Refresh();
    await TagStore.Refresh();
    await UserStore.Refresh();
  }
}