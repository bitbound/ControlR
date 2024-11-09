namespace ControlR.Web.Client.Services.Stores;

public interface ITagStore : IStoreBase<TagResponseDto>
{
}

public class TagStore(IControlrApi controlrApi, ISnackbar snackbar, ILogger<TagStore> logger)
  : StoreBase<TagResponseDto>(controlrApi, snackbar, logger), ITagStore
{
  // TODO: Convert DTO to view model.
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
      Cache.AddOrUpdate(tag.Id, tag, (_, _) => tag);
    }
  }
}