using ControlR.Web.Client.ViewModels;

namespace ControlR.Web.Client.Services.Stores;

public interface ITagStore : IStoreBase<TagViewModel>
{}

public class TagStore(IControlrApi controlrApi, ISnackbar snackbar, ILogger<TagStore> logger)
  : StoreBase<TagViewModel>(controlrApi, snackbar, logger), ITagStore
{
  protected override async Task RefreshImpl()
  {
    var getResult = await ControlrApi.GetAllTags();
    if (!getResult.IsSuccess)
    {
      Snackbar.Add(getResult.Reason, Severity.Error);
      return;
    }

    foreach (var tag in getResult.Value)
    {
      var tagVm = new TagViewModel(tag);
      Cache.AddOrUpdate(tag.Id, tagVm, (_, _) => tagVm);
    }
  }
}