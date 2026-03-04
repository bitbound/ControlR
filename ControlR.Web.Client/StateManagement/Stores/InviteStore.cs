namespace ControlR.Web.Client.StateManagement.Stores;

public interface IInviteStore : IStoreBase<TenantInviteResponseDto>
{ }

public class InviteStore(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<StoreBase<TenantInviteResponseDto>> logger)
  : StoreBase<TenantInviteResponseDto>(controlrApi, snackbar, logger), IInviteStore
{
  private readonly IControlrApi _controlrApi = controlrApi;

  protected override Guid GetItemId(TenantInviteResponseDto dto)
  {
    return dto.Id;
  }

  protected override async Task RefreshImpl()
  {
    Cache.Clear();
    var getResult = await _controlrApi.Invites.GetPendingTenantInvites();
    if (!getResult.IsSuccess)
    {
      Snackbar.Add(getResult.Reason, Severity.Error);
      return;
    }

    foreach (var invite in getResult.Value)
    {
      Cache.AddOrUpdate(invite.Id, invite, (_, _) => invite);
    }
  }
}
