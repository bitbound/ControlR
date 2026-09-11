using Microsoft.AspNetCore.Components.Authorization;
using PADtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

namespace ControlR.Web.Client.StateManagement.Stores;

public interface IPermissionCatalogStore : IStoreBase<PADtos.PermissionCatalogEntryDto>
{
}

public class PermissionCatalogStore(
  IControlrApi controlrApi,
  AuthenticationStateProvider authState,
  ISnackbar snackbar,
  ILogger<PermissionCatalogStore> logger) : StoreBase<PADtos.PermissionCatalogEntryDto>(controlrApi, snackbar, logger), IPermissionCatalogStore
{
  protected override Guid GetItemId(PADtos.PermissionCatalogEntryDto dto)
  {
    return StableId(dto.Name);
  }

  protected override IEnumerable<PADtos.PermissionCatalogEntryDto> OrderItems(IEnumerable<PADtos.PermissionCatalogEntryDto> items)
  {
    return items.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase);
  }

  protected override async Task RefreshImpl()
  {
    var state = await authState.GetAuthenticationStateAsync();
    if (!state.User.TryGetTenantId(out var tenantId))
    {
      Snackbar.Add("No tenant is associated with the signed-in user.", Severity.Error);
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
