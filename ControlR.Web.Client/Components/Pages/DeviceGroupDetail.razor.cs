using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.Web.Client.Components.Pages;

public partial class DeviceGroupDetail : ComponentBase
{
  private InternalDtos.DeviceGroupDetailDto? _group;
  private bool _loading;

  [Inject]
  public required IClipboardManager ClipboardManager { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDialogService DialogService { get; init; }

  [Parameter]
  public Guid Id { get; set; }

  [Inject]
  public required NavigationManager Navigation { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  protected override async Task OnInitializedAsync()
  {
    await LoadGroup();
  }

  private async Task AddMembers()
  {
    if (_group is null)
    {
      return;
    }

    var parameters = new DialogParameters<AddDeviceGroupMembersDialog>
    {
      { x => x.GroupId, _group.Id },
      { x => x.ExcludeDeviceIds, [.. _group.Members.Select(m => m.DeviceId)] }
    };

    var options = new DialogOptions
    {
      FullWidth = true,
      MaxWidth = MaxWidth.Medium
    };

    var dialog = await DialogService.ShowAsync<AddDeviceGroupMembersDialog>("Add Devices", parameters, options);
    var result = await dialog.Result;

    if (result is not null && !result.Canceled)
    {
      await LoadGroup();
    }
  }

  private async Task CopyGroupId()
  {
    if (_group is null)
    {
      return;
    }

    await ClipboardManager.SetText(_group.Id.ToString());
    Snackbar.Add("Copied to clipboard", Severity.Success);
  }

  private async Task EditGroup()
  {
    if (_group is null)
    {
      return;
    }

    var parameters = new DialogParameters<EditGroupDialog>
    {
      { x => x.Name, _group.Name },
      { x => x.Description, _group.Description }
    };

    var options = new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Small };
    var dialog = await DialogService.ShowAsync<EditGroupDialog>("Edit Device Group", parameters, options);
    var result = await dialog.Result;

    if (result is null || result.Canceled || result.Data is not EditGroupDialogResult editResult)
    {
      return;
    }

    var updateResult = await ControlrApi.Internal.DeviceGroups.Update(
      _group.Id,
      new InternalDtos.UpdateDeviceGroupRequestDto(editResult.Name, editResult.Description));

    if (!updateResult.IsSuccess)
    {
      Snackbar.Add(updateResult.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("Device group updated", Severity.Success);
    await LoadGroup();
  }

  private async Task LoadGroup()
  {
    _loading = true;
    StateHasChanged();

    try
    {
      var result = await ControlrApi.Internal.DeviceGroups.Get(Id);
      if (result.IsSuccess)
      {
        _group = result.Value;
      }
      else
      {
        Snackbar.Add(result.Reason, Severity.Error);
      }
    }
    finally
    {
      _loading = false;
      StateHasChanged();
    }
  }

  private async Task RefreshGroup()
  {
    await LoadGroup();
    Snackbar.Add("Device group refreshed", Severity.Success);
  }

  private async Task RemoveMember(InternalDtos.DeviceGroupMemberDto member)
  {
    if (_group is null)
    {
      return;
    }

    var confirmed = await DialogService.ShowMessageBoxAsync(
      "Remove Member",
      $"Remove \"{member.DeviceName}\" from this group?",
      "Remove", "Cancel");

    if (!confirmed.GetValueOrDefault())
    {
      return;
    }

    var result = await ControlrApi.Internal.DeviceGroups.RemoveMembers(
      _group.Id, new InternalDtos.RemoveDeviceGroupMembersRequestDto([member.DeviceId]));

    if (!result.IsSuccess)
    {
      Snackbar.Add(result.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("Member removed", Severity.Success);
    await LoadGroup();
  }
}
