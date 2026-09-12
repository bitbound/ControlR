using Microsoft.AspNetCore.Components.Authorization;

namespace ControlR.Web.Client.StateManagement.Stores;

public interface ITagStore : IStoreBase<TagViewModel>
{ }

public class TagStore(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<TagStore> logger,
  AuthenticationStateProvider authState)
  : StoreBase<TagViewModel>(controlrApi, snackbar, logger), ITagStore
{
  private readonly AuthenticationStateProvider _authState = authState;

  protected override Guid GetItemId(TagViewModel dto)
  {
    return dto.Id;
  }

  protected override IEnumerable<TagViewModel> OrderItems(IEnumerable<TagViewModel> items)
  {
    return items.OrderBy(t => t.Name);
  }

  protected override async Task RefreshImpl()
  {
    if (await _authState.GetTenantIdAsync() is not { } tenantId)
    {
      return;
    }

    var getResult = await ControlrApi.V1.Tags.GetAllTags(tenantId, includeLinkedIds: true);
    if (!getResult.IsSuccess)
    {
      Snackbar.Add(getResult.Reason, Severity.Error);
      return;
    }

    var vms = getResult.Value.Items.Select(tag => new TagViewModel(tag));
    SetItems(vms);
  }
}