using Microsoft.AspNetCore.Components.Authorization;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserGroups;

namespace ControlR.Web.Client.Components.Pages;

public partial class UserGroupDetail : ComponentBase
{
  private UserGroupDetailDto? _group;
  private bool _loading;
  private Guid _tenantId;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

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
    if (await AuthState.GetTenantIdAsync(Snackbar) is not { } tenantId)
    {
      return;
    }

    _tenantId = tenantId;
    await LoadGroup();
  }

  private async Task AddMembers()
  {
    if (_group is null)
    {
      return;
    }

    var parameters = new DialogParameters<AddUserGroupMembersDialog>
    {
      { x => x.GroupId, _group.Id },
      { x => x.ExcludeUserIds, [.. _group.Members.Select(m => m.UserId)] }
    };

    var options = new DialogOptions
    {
      FullWidth = true,
      MaxWidth = MaxWidth.Medium
    };

    var dialog = await DialogService.ShowAsync<AddUserGroupMembersDialog>("Add Users", parameters, options);
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
    var dialog = await DialogService.ShowAsync<EditGroupDialog>("Edit User Group", parameters, options);
    var result = await dialog.Result;

    if (result is null || result.Canceled || result.Data is not EditGroupDialogResult editResult)
    {
      return;
    }

    var updateResult = await ControlrApi.V1.UserGroups.UpdateUserGroup(
      _group.Id,
      _tenantId,
      new UpdateUserGroupRequestDto(editResult.Name, editResult.Description));

    if (!updateResult.IsSuccess)
    {
      Snackbar.Add(updateResult.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("User group updated", Severity.Success);
    await LoadGroup();
  }

  private async Task LoadGroup()
  {
    _loading = true;
    StateHasChanged();

    try
    {
      var result = await ControlrApi.V1.UserGroups.GetUserGroup(Id, _tenantId);
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
    Snackbar.Add("User group refreshed", Severity.Success);
  }

  private async Task RemoveMember(UserGroupMemberDto member)
  {
    if (_group is null)
    {
      return;
    }

    var confirmed = await DialogService.ShowMessageBoxAsync(
      "Remove Member",
      $"Remove \"{member.UserName}\" from this group?",
      "Remove", "Cancel");

    if (!confirmed.GetValueOrDefault())
    {
      return;
    }

    var result = await ControlrApi.V1.UserGroups.RemoveUserGroupMembers(
      _group.Id, _tenantId, new RemoveUserGroupMembersRequestDto([member.UserId]));

    if (!result.IsSuccess)
    {
      Snackbar.Add(result.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("Member removed", Severity.Success);
    await LoadGroup();
  }
}