using Microsoft.AspNetCore.Components.Authorization;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Invites;

namespace ControlR.Web.Client.StateManagement.Stores;

public interface IInviteStore : IStoreBase<InviteResponseDto>
{ }

public class InviteStore(
  IControlrApi controlrApi,
  AuthenticationStateProvider authState,
  ISnackbar snackbar,
  ILogger<StoreBase<InviteResponseDto>> logger)
  : StoreBase<InviteResponseDto>(controlrApi, snackbar, logger), IInviteStore
{
  private readonly AuthenticationStateProvider _authState = authState;
  private readonly IControlrApi _controlrApi = controlrApi;

  protected override Guid GetItemId(InviteResponseDto dto)
  {
    return dto.Id;
  }

  protected override async Task RefreshImpl()
  {
    if (await _authState.GetTenantIdAsync(Snackbar) is not { } tenantId)
    {
      return;
    }

    var getResult = await _controlrApi.V1.Invites.GetInvites(tenantId);
    if (!getResult.IsSuccess)
    {
      Snackbar.Add(getResult.Reason, Severity.Error);
      return;
    }

    SetItems(getResult.Value.Items);
  }
}
