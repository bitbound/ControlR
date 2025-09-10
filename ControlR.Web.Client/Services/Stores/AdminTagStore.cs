namespace ControlR.Web.Client.Services.Stores;

public interface IAdminTagStore : IStoreBase<TagViewModel>
{ }

public class AdminTagStore(IControlrApi controlrApi, ISnackbar snackbar, ILogger<AdminTagStore> logger)
  : StoreBase<TagViewModel>(controlrApi, snackbar, logger), IAdminTagStore
{
  protected override async Task RefreshImpl()
  {
    Cache.Clear();
    var getResult = await ControlrApi.GetAllTags(includeLinkedIds: true);
    if (!getResult.IsSuccess)
    {
      Snackbar.Add(getResult.Reason, Severity.Error);
      return;
    }

    foreach (var tag in getResult.Value)
    {
      var vm = new TagViewModel(tag);
      Cache.AddOrUpdate(vm.Id, vm, (_, _) => vm);
    }
  }
}