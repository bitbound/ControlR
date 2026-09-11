using Microsoft.AspNetCore.Components.Authorization;
using UsersDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Users;

namespace ControlR.Web.Client.StateManagement.Stores;

public interface IUserStore : IStoreBase<UsersDtos.UserResponseDto>
{
}

public class UserStore(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<UserStore> logger,
  AuthenticationStateProvider authState) : StoreBase<UsersDtos.UserResponseDto>(controlrApi, snackbar, logger), IUserStore
{
  private readonly AuthenticationStateProvider _authState = authState;

  protected override Guid GetItemId(UsersDtos.UserResponseDto dto)
  {
    return dto.Id;
  }

  protected override async Task RefreshImpl()
  {
    var state = await _authState.GetAuthenticationStateAsync();
    if (!state.User.TryGetTenantId(out var tenantId))
    {
      return;
    }

    var getResult = await ControlrApi.V1.Users.GetAllUsers(tenantId);
    if (!getResult.IsSuccess)
    {
      Snackbar.Add(getResult.Reason, Severity.Error);
      return;
    }
    SetItems(getResult.Value.Items);
  }
}
