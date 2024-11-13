using ControlR.Web.Client.ViewModels;

namespace ControlR.Web.Client.Services.Stores;

public interface IRoleStore : IStoreBase<RoleViewModel>
{ }

internal class RoleStore : StoreBase<RoleViewModel>, IRoleStore
{
  public RoleStore(
    IControlrApi controlrApi,
    ISnackbar snackbar,
    ILogger<RoleStore> logger)
    : base(controlrApi, snackbar, logger)
  {

  }

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
      var vm = new RoleViewModel(role);
      Cache.AddOrUpdate(vm.Id, vm, (_, _) => vm);
    }
  }
}