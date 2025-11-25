using System.Collections.Immutable;
using ControlR.Web.Client.StateManagement.Stores;
using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Components.Permissions;

public partial class DevicesTabContent : ComponentBase, IDisposable
{
  private ImmutableArray<IDisposable>? _changeHandlers;
  private DeviceDto? _selectedDevice;
  private string _tagSearchPattern = string.Empty;

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required ILogger<DevicesTabContent> Logger { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required IAdminTagStore TagStore { get; init; }

  private IOrderedEnumerable<TagViewModel> FilteredTags =>
    TagStore.Items
      .Where(x => x.Name.Contains(_tagSearchPattern, StringComparison.OrdinalIgnoreCase))
      .OrderBy(x => x.Name);

  public void Dispose()
  {
    _changeHandlers?.DisposeAll();
    GC.SuppressFinalize(this);
  }

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
    _changeHandlers =
    [
      TagStore.RegisterChangeHandler(this, async () => await InvokeAsync(StateHasChanged))
    ];
  }

  private async Task SetDeviceTag(bool isToggled, Guid deviceId, TagViewModel tag)
  {
    try
    {
      if (isToggled)
      {
        var addResult = await ControlrApi.AddDeviceTag(deviceId, tag.Id);
        if (!addResult.IsSuccess)
        {
          Snackbar.Add(addResult.Reason, Severity.Error);
          return;
        }
        tag.DeviceIds.Add(deviceId);
      }
      else
      {
        var removeResult = await ControlrApi.RemoveDeviceTag(deviceId, tag.Id);
        if (!removeResult.IsSuccess)
        {
          Snackbar.Add(removeResult.Reason, Severity.Error);
          return;
        }
        tag.DeviceIds.Remove(deviceId);
      }

      await TagStore.InvokeItemsChanged();

      Snackbar.Add(isToggled
        ? "Tag added"
        : "Tag removed", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while setting tag.");
      Snackbar.Add("An error occurred while setting tag", Severity.Error);
    }
  }
}