using Microsoft.AspNetCore.Components.Authorization;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

namespace ControlR.Web.Client.StateManagement.Stores;

public interface IPermissionCatalogStore : IStoreBase<PermissionCatalogEntryDto>
{
}

public class PermissionCatalogStore(
  IControlrApi controlrApi,
  AuthenticationStateProvider authState,
  ISnackbar snackbar,
  ILogger<PermissionCatalogStore> logger) : StoreBase<PermissionCatalogEntryDto>(controlrApi, snackbar, logger), IPermissionCatalogStore
{
  protected override Guid GetItemId(PermissionCatalogEntryDto dto)
  {
    return StableId(dto.Name);
  }

  protected override IEnumerable<PermissionCatalogEntryDto> OrderItems(IEnumerable<PermissionCatalogEntryDto> items)
  {
    return items.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase);
  }

  protected override async Task RefreshImpl()
  {
    if (await authState.GetTenantId(Snackbar) is not { } tenantId)
    {
      return;
    }

    var result = await ControlrApi.V1.PermissionAssignments.GetCatalog(tenantId);
    if (!result.IsSuccess)
    {
      Snackbar.Add(result.Reason, Severity.Error);
      return;
    }

    SetItems(result.Value.Items);
  }

  private static Guid StableId(string name)
  {
    var bytes = System.Text.Encoding.UTF8.GetBytes(name);
    var hash = System.Security.Cryptography.SHA256.HashData(bytes);
    return new Guid([.. hash[..16]]);
  }
}
