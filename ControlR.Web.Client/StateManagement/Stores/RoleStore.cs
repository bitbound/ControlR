namespace ControlR.Web.Client.StateManagement.Stores;

public interface IRoleStore : IStoreBase<RoleViewModel>
{ }

internal class RoleStore(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<RoleStore> logger)
  : StoreBase<RoleViewModel>(controlrApi, snackbar, logger), IRoleStore
{
  protected override async Task RefreshImpl()
  {
    Cache.Clear();
    var getResult = await ControlrApi.GetAllRoles();
    if (!getResult.IsSuccess)
    {
      Snackbar.Add(getResult.Reason, Severity.Error);
      return;
    }

    foreach (var role in getResult.Value)
    {
      var vm = new RoleViewModel(role);
      Cache.AddOrUpdate(vm.Id, vm, (_, _) => vm);
    }
  }
}