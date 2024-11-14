using ControlR.Web.Client.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Collections.Immutable;

namespace ControlR.Web.Client.Components.Permissions;

public partial class UsersTabContent : ComponentBase, IDisposable
{
  private ImmutableArray<IDisposable>? _changeHandlers;
  private Guid _currentUserId;
  private bool _isServerAdmin;
  private string _roleSearchPattern = string.Empty;
  private UserResponseDto? _selectedUser;
  private string _tagSearchPattern = string.Empty;
  private string _userSearchPattern = string.Empty;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required ILogger<UsersTabContent> Logger { get; init; }

  [Inject]
  public required IRoleStore RoleStore { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required ITagStore TagStore { get; init; }

  [Inject]
  public required IUserStore UserStore { get; init; }

  private IOrderedEnumerable<RoleViewModel> FilteredRoles
  {
    get
    {
      var query = RoleStore.Items
        .Where(x => x.Name.Contains(_roleSearchPattern, StringComparison.OrdinalIgnoreCase));

      if (!_isServerAdmin)
      {
        query = query.Where(x => x.Name != RoleNames.ServerAdministrator);
      }

      if (_selectedUser?.Id == _currentUserId)
      {
        query = query.Where(x => 
          x.Name != RoleNames.ServerAdministrator && x.Name != RoleNames.TenantAdministrator);
      }

      return query.OrderBy(x => x.Name);
    }
  }

  private IOrderedEnumerable<TagViewModel> FilteredTags =>
    TagStore.Items
      .Where(x => x.Name.Contains(_tagSearchPattern, StringComparison.OrdinalIgnoreCase))
      .OrderBy(x => x.Name);

  private IOrderedEnumerable<UserResponseDto> FilteredUsers =>
    UserStore.Items
      .Where(x => x.UserName?.Contains(_userSearchPattern, StringComparison.OrdinalIgnoreCase) == true)
      .OrderBy(x => x.UserName);


  public void Dispose()
  {
    _changeHandlers?.DisposeAll();
    GC.SuppressFinalize(this);
  }

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
    var state = await AuthState.GetAuthenticationStateAsync();
    if (state.User.TryGetUserId(out var userId))
    {
      _currentUserId = userId;
    }
    _isServerAdmin = state.User.IsInRole(RoleNames.ServerAdministrator);
    _changeHandlers =
    [
      TagStore.RegisterChangeHandler(this, async () => await InvokeAsync(StateHasChanged)),
      UserStore.RegisterChangeHandler(this, async () => await InvokeAsync(StateHasChanged)),
      RoleStore.RegisterChangeHandler(this, async () => await InvokeAsync(StateHasChanged))
    ];
  }

  private async Task SetUserRole(bool isToggled, UserResponseDto user, RoleViewModel role)
  {
    try
    {
      if (isToggled)
      {
        var addResult = await ControlrApi.AddUserRole(user.Id, role.Id);
        if (!addResult.IsSuccess)
        {
          Snackbar.Add(addResult.Reason, Severity.Error);
          return;
        }
        role.UserIds.Add(user.Id);
      }
      else
      {
        var removeResult = await ControlrApi.RemoveUserRole(user.Id, role.Id);
        if (!removeResult.IsSuccess)
        {
          Snackbar.Add(removeResult.Reason, Severity.Error);
          return;
        }
        role.UserIds.Remove(user.Id);
      }

      await TagStore.InvokeItemsChanged();

      Snackbar.Add(isToggled
        ? "Role added"
        : "Role removed", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while setting role.");
      Snackbar.Add("An error occurred while setting role", Severity.Error);
    }
  }

  private async Task SetUserTag(bool isToggled, UserResponseDto user, TagViewModel tag)
  {
    try
    {
      if (isToggled)
      {
        var addResult = await ControlrApi.AddUserTag(user.Id, tag.Id);
        if (!addResult.IsSuccess)
        {
          Snackbar.Add(addResult.Reason, Severity.Error);
          return;
        }
        tag.UserIds.Add(user.Id);
      }
      else
      {
        var removeResult = await ControlrApi.RemoveUserTag(user.Id, tag.Id);
        if (!removeResult.IsSuccess)
        {
          Snackbar.Add(removeResult.Reason, Severity.Error);
          return;
        }
        tag.UserIds.Remove(user.Id);
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
