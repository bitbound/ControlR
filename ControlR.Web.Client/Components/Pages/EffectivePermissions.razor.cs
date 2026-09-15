using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.EffectivePermissions;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControlR.Web.Client.Components.Pages;

public partial class EffectivePermissions : ComponentBase
{
  private List<PermissionCatalogEntryDto> _catalog = [];
  private string _permissionName = string.Empty;
  private PermissionPrincipalKind _principalKind = PermissionPrincipalKind.User;
  private EffectivePermissionQueryResponseDto? _result;
  private Guid? _scopeId;
  private PermissionScopeKind _scopeKind = PermissionScopeKind.Tenant;
  private Guid? _selectedPrincipalId;
  private Guid _tenantId;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required ILogger<EffectivePermissions> Logger { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  protected override async Task OnInitializedAsync()
  {
    try
    {
      if (await AuthState.GetTenantId(Snackbar) is not { } tenantId)
      {
        return;
      }

      _tenantId = tenantId;

      var result = await ControlrApi.V1.PermissionAssignments.GetCatalog(_tenantId);
      if (result.IsSuccess)
      {
        _catalog = [.. result.Value.Items];
      }
      else
      {
        Snackbar.Add(result.Reason, Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Failed to load the permission catalog.");
      Snackbar.Add("Failed to load the permission catalog.", Severity.Error);
    }
  }

  private async Task Query()
  {
    if (_selectedPrincipalId is not Guid principalId)
    {
      Snackbar.Add("Select a principal first", Severity.Error);
      return;
    }

    if (string.IsNullOrWhiteSpace(_permissionName))
    {
      Snackbar.Add("Permission name is required", Severity.Error);
      return;
    }

    try
    {
      var result = await ControlrApi.V1.EffectivePermissions.GetEffectivePermission(
        principalId,
        _tenantId,
        _principalKind,
        _permissionName,
        _scopeKind,
        _scopeId);

      if (!result.IsSuccess)
      {
        Snackbar.Add(result.Reason, Severity.Error);
        return;
      }

      _result = result.Value;
      StateHasChanged();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Failed to query effective permissions.");
      Snackbar.Add("Failed to query effective permissions.", Severity.Error);
    }
  }
}
