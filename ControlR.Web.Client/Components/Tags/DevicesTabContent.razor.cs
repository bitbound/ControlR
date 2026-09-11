using System.Collections.Immutable;
using Microsoft.AspNetCore.Components.Authorization;
using DeviceTagsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceTags;

namespace ControlR.Web.Client.Components.Tags;

public partial class DevicesTabContent : ComponentBase, IDisposable
{
  private ImmutableArray<IDisposable>? _changeHandlers;
  private DeviceResponseDto? _selectedDevice;
  private string _tagSearchPattern = string.Empty;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required ILogger<DevicesTabContent> Logger { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required ITagStore TagStore { get; init; }

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
      var state = await AuthState.GetAuthenticationStateAsync();
      if (!state.User.TryGetTenantId(out var tenantId))
      {
        Snackbar.Add("No tenant found for the current user", Severity.Error);
        return;
      }

      if (isToggled)
      {
        var addRequest = new DeviceTagsDtos.DeviceTagAddRequestDto(deviceId, tag.Id);
        var addResult = await ControlrApi.V1.DeviceTags.AddDeviceTag(tenantId, addRequest);
        if (!addResult.IsSuccess)
        {
          Snackbar.Add(addResult.Reason, Severity.Error);
          return;
        }
        tag.DeviceIds.Add(deviceId);
      }
      else
      {
        var removeResult = await ControlrApi.V1.DeviceTags.RemoveDeviceTag(deviceId, tag.Id, tenantId);
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
