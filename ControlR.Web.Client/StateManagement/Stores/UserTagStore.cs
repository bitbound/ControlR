namespace ControlR.Web.Client.StateManagement.Stores;
public interface IUserTagStore : IStoreBase<TagViewModel>
{ }

public class UserTagStore(IControlrApi controlrApi, ISnackbar snackbar, ILogger<AdminTagStore> logger)
  : StoreBase<TagViewModel>(controlrApi, snackbar, logger), IUserTagStore
{
  protected override async Task RefreshImpl()
  {
    Cache.Clear();
    var getResult = await ControlrApi.GetAllowedTags();
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