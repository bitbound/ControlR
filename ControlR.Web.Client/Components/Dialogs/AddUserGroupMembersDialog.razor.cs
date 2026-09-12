using Microsoft.AspNetCore.Components.Authorization;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserGroups;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Users;

namespace ControlR.Web.Client.Components.Dialogs;

public partial class AddUserGroupMembersDialog : ComponentBase
{
  private List<UserResponseDto> _allUsers = [];
  private bool _loading;
  private string _searchText = string.Empty;
  private HashSet<Guid> _selectedIds = [];
  private Guid _tenantId;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Parameter]
  public required HashSet<Guid> ExcludeUserIds { get; set; }

  [Parameter]
  public required Guid GroupId { get; set; }

  [CascadingParameter]
  public required IMudDialogInstance MudDialog { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  private List<UserResponseDto> FilteredUsers
  {
    get
    {
      var candidates = _allUsers.Where(u => !ExcludeUserIds.Contains(u.Id));

      if (!string.IsNullOrWhiteSpace(_searchText))
      {
        candidates = candidates.Where(u =>
          (u.UserName?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
          (u.Email?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
          (u.DisplayName?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false));
      }

      return [.. candidates.OrderBy(u => u.UserName)];
    }
  }

  protected override async Task OnInitializedAsync()
  {
    _loading = true;
    StateHasChanged();

    try
    {
      var state = await AuthState.GetAuthenticationStateAsync();
      if (!state.User.TryGetTenantId(out var tenantId))
      {
        Snackbar.Add("No tenant is associated with the signed-in user.", Severity.Error);
        return;
      }

      _tenantId = tenantId;

      var result = await ControlrApi.V1.Users.GetAllUsers(tenantId);
      if (result.IsSuccess)
      {
        _allUsers = [.. result.Value.Items];
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

  private async Task Add()
  {
    var result = await ControlrApi.V1.UserGroups.AddUserGroupMembers(
      GroupId, _tenantId, new AddUserGroupMembersRequestDto([.. _selectedIds]));

    if (!result.IsSuccess)
    {
      Snackbar.Add(result.Reason, Severity.Error);
      return;
    }

    Snackbar.Add($"Added {_selectedIds.Count} user(s)", Severity.Success);
    MudDialog.Close(DialogResult.Ok(true));
  }

  private void Cancel() => MudDialog.Cancel();

  private void ToggleSelection(Guid userId, bool isSelected)
  {
    if (isSelected)
    {
      _selectedIds.Add(userId);
    }
    else
    {
      _selectedIds.Remove(userId);
    }
  }
}