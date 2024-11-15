using ControlR.Web.Client.ViewModels;

namespace ControlR.Web.Client.Services.Stores;

public interface IRoleStore : IStoreBase<RoleResponseDto>
{}

internal class RoleStore(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<RoleStore> logger)
  : StoreBase<RoleResponseDto>(controlrApi, snackbar, logger), IRoleStore
{
  protected override async Task RefreshImpl()
  {
    var getResult = await ControlrApi.GetAllRoles();
    if (!getResult.IsSuccess)
    {
      Snackbar.Add(getResult.Reason, Severity.Error);
      return;
    }

    foreach (var role in getResult.Value)
    {
      Cache.AddOrUpdate(role.Id, role, (_, _) => role);
    }
  }
}