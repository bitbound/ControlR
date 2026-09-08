using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.Web.Client.Components.Pages;

public partial class EffectivePermissions : ComponentBase
{
  private List<InternalDtos.PermissionCatalogEntryDto> _catalog = [];
  private string _permissionName = string.Empty;
  private PermissionPrincipalKind _principalKind = PermissionPrincipalKind.User;
  private InternalDtos.EffectivePermissionQueryResponseDto? _result;
  private Guid? _scopeId;
  private PermissionScopeKind _scopeKind = PermissionScopeKind.Tenant;
  private Guid? _selectedPrincipalId;

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
      var result = await ControlrApi.Internal.PermissionAssignments.GetCatalog();
      if (result.IsSuccess)
      {
        _catalog = [.. result.Value];
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

    var request = new InternalDtos.EffectivePermissionQueryRequestDto(
      _principalKind,
      principalId,
      _permissionName,
      _scopeKind,
      _scopeId);

    try
    {
      var result = await ControlrApi.Internal.EffectivePermissions.Query(request);
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
