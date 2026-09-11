using Microsoft.AspNetCore.Components.Authorization;
using UsersDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Users;

namespace ControlR.Web.Client.Components.Pages;

public partial class Users : ComponentBase
{
  private readonly Dictionary<string, SortDefinition<UsersDtos.UserResponseDto>> _sortDefinitions = new()
  {
    ["UserName"] = new SortDefinition<UsersDtos.UserResponseDto>(
      SortBy: nameof(UsersDtos.UserResponseDto.UserName),
      Descending: false,
      Index: 0,
      SortFunc: x => x.UserName)
  };

  private Guid? _currentUserId;
  private bool _loading;
  private string _searchString = string.Empty;
  private Guid? _tenantId;
  private IEnumerable<UsersDtos.UserResponseDto> _users = [];

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required IClipboardManager ClipboardManager { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDialogService DialogService { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  private Func<UsersDtos.UserResponseDto, bool> QuickFilter => user =>
  {
    if (string.IsNullOrWhiteSpace(_searchString))
    {
      return true;
    }

    return (user.UserName?.Contains(_searchString, StringComparison.OrdinalIgnoreCase) ?? false) ||
           (user.Email?.Contains(_searchString, StringComparison.OrdinalIgnoreCase) ?? false) ||
           (user.DisplayName?.Contains(_searchString, StringComparison.OrdinalIgnoreCase) ?? false);
  };

  protected override async Task OnInitializedAsync()
  {
    var state = await AuthState.GetAuthenticationStateAsync();
    if (state.User.TryGetUserId(out var currentUserId))
    {
      _currentUserId = currentUserId;
    }

    if (state.User.TryGetTenantId(out var tenantId))
    {
      _tenantId = tenantId;
    }

    await Refresh();
  }

  private async Task CopyId(Guid id)
  {
    await ClipboardManager.SetText(id.ToString());
    Snackbar.Add("Copied to clipboard", Severity.Success);
  }

  private async Task DeleteUser(UsersDtos.UserResponseDto user)
  {
    var confirmed = await DialogService.ShowMessageBoxAsync(
      "Delete User",
      $"Are you sure you want to delete \"{user.UserName}\"? This will permanently remove the user and all of their access.",
      "Delete", "Cancel");

    if (!confirmed.GetValueOrDefault())
    {
      return;
    }

    if (_tenantId is not { } tenantId)
    {
      Snackbar.Add("No tenant is associated with the signed-in user.", Severity.Error);
      return;
    }

    var result = await ControlrApi.V1.Users.DeleteUser(user.Id, tenantId);
    if (!result.IsSuccess)
    {
      Snackbar.Add(result.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("User deleted", Severity.Success);
    await Refresh();
  }

  private async Task EditPermissions(UsersDtos.UserResponseDto user)
  {
    var parameters = new DialogParameters<PermissionAssignmentPanelDialog>
    {
      { x => x.PrincipalKind, PermissionPrincipalKind.User },
      { x => x.PrincipalId, user.Id }
    };

    await DialogService.ShowAsync<PermissionAssignmentPanelDialog>(
      $"Permissions: {user.UserName ?? user.Email ?? user.Id.ToString()}",
      parameters,
      PermissionAssignmentPanelDialog.DefaultOptions);
  }

  private async Task Refresh()
  {
    _loading = true;
    StateHasChanged();

    try
    {
      if (_tenantId is not { } tenantId)
      {
        Snackbar.Add("No tenant is associated with the signed-in user.", Severity.Error);
        return;
      }

      var result = await ControlrApi.V1.Users.GetAllUsers(tenantId);
      if (result.IsSuccess)
      {
        _users = result.Value.Items;
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
}
