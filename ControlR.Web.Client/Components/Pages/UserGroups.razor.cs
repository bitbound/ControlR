using Microsoft.AspNetCore.Components.Authorization;
using UGDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserGroups;

namespace ControlR.Web.Client.Components.Pages;

public partial class UserGroups : ComponentBase
{
  private IEnumerable<UGDtos.UserGroupDto> _groups = [];
  private bool _loading;
  private string _searchString = string.Empty;
  private Guid _tenantId;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required IClipboardManager ClipboardManager { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDialogService DialogService { get; init; }

  [Inject]
  public required NavigationManager Navigation { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  private Func<UGDtos.UserGroupDto, bool> QuickFilter => group =>
  {
    if (string.IsNullOrWhiteSpace(_searchString))
    {
      return true;
    }

    return group.Name.Contains(_searchString, StringComparison.OrdinalIgnoreCase) ||
           (group.Description?.Contains(_searchString, StringComparison.OrdinalIgnoreCase) ?? false);
  };

  protected override async Task OnInitializedAsync()
  {
    var state = await AuthState.GetAuthenticationStateAsync();
    if (!state.User.TryGetTenantId(out var tenantId))
    {
      Snackbar.Add("No tenant is associated with the signed-in user.", Severity.Error);
      return;
    }

    _tenantId = tenantId;
    await Refresh();
  }

  private async Task CopyId(Guid id)
  {
    await ClipboardManager.SetText(id.ToString());
    Snackbar.Add("Copied to clipboard", Severity.Success);
  }

  private async Task CreateGroup()
  {
    var name = await DialogService.ShowPrompt(
      "Create User Group",
      "Enter a name for the new user group.",
      "Group name");

    if (string.IsNullOrWhiteSpace(name))
    {
      return;
    }

    var result = await ControlrApi.V1.UserGroups.CreateUserGroup(
      _tenantId, new UGDtos.CreateUserGroupRequestDto(name, null));

    if (!result.IsSuccess)
    {
      Snackbar.Add(result.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("User group created", Severity.Success);
    await Refresh();
  }

  private async Task DeleteGroup(UGDtos.UserGroupDto group)
  {
    var confirmed = await DialogService.ShowMessageBoxAsync(
      "Delete User Group",
      $"Are you sure you want to delete \"{group.Name}\"? This will also remove any permission assignments for this group.",
      "Delete", "Cancel");

    if (!confirmed.GetValueOrDefault())
    {
      return;
    }

    var result = await ControlrApi.V1.UserGroups.DeleteUserGroup(group.Id, _tenantId);
    if (!result.IsSuccess)
    {
      Snackbar.Add(result.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("User group deleted", Severity.Success);
    await Refresh();
  }

  private async Task EditPermissions(UGDtos.UserGroupDto group)
  {
    var parameters = new DialogParameters<PermissionAssignmentPanelDialog>
    {
      { x => x.PrincipalKind, PermissionPrincipalKind.UserGroup },
      { x => x.PrincipalId, group.Id }
    };

    await DialogService.ShowAsync<PermissionAssignmentPanelDialog>(
      $"Permissions: {group.Name}",
      parameters,
      PermissionAssignmentPanelDialog.DefaultOptions);
  }

  private async Task Refresh()
  {
    _loading = true;
    StateHasChanged();

    try
    {
      var result = await ControlrApi.V1.UserGroups.GetAllUserGroups(_tenantId);
      if (result.IsSuccess)
      {
        _groups = result.Value.Items;
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

  private string TruncateId(Guid id)
  {
    return $"{id.ToString()[..8]}...";
  }

  private void ViewGroup(UGDtos.UserGroupDto group)
  {
    Navigation.NavigateTo($"/user-groups/{group.Id}");
  }
}