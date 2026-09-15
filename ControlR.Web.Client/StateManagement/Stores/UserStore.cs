using Microsoft.AspNetCore.Components.Authorization;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Users;

namespace ControlR.Web.Client.StateManagement.Stores;

public interface IUserStore : IStoreBase<UserResponseDto>
{
}

public class UserStore(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<UserStore> logger,
  AuthenticationStateProvider authState) : StoreBase<UserResponseDto>(controlrApi, snackbar, logger), IUserStore
{
  private readonly AuthenticationStateProvider _authState = authState;

  protected override Guid GetItemId(UserResponseDto dto)
  {
    return dto.Id;
  }

  protected override async Task RefreshImpl()
  {
    if (await _authState.GetTenantId() is not { } tenantId)
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
