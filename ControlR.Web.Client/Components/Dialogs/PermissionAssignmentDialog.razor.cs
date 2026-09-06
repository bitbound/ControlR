using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.Web.Client.Components.Dialogs;

public partial class PermissionAssignmentDialog : ComponentBase
{
  private IReadOnlyList<InternalDtos.PermissionCatalogEntryDto> _catalog = [];
  private PermissionEffect _effect = PermissionEffect.Allow;
  private bool _isEnabled = true;
  private string _notes = string.Empty;
  private string _permissionName = string.Empty;
  private Guid? _scopeId;
  private PermissionScopeKind _scopeKind = PermissionScopeKind.Tenant;
  private InternalDtos.PermissionCatalogEntryDto? _selectedPermission;

  public static DialogOptions DefaultOptions => new()
  {
    BackdropClick = false
  };

  [Parameter]
  public ServiceAccountKind AccountKind { get; set; } = ServiceAccountKind.Tenant;

  /// <summary>
  /// When false, the Server scope kind is hidden from the scope dropdown so callers without
  /// <see cref="PermissionNames.ServerPermissionsWrite"/> cannot select it. The server remains
  /// authoritative — <see cref="PermissionAssignmentManager.ValidateWriteAuthority"/> rejects
  /// Server-scope writes at the API boundary regardless.
  /// </summary>
  [Parameter]
  public bool CanManageServerScope { get; set; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Parameter]
  public InternalDtos.PermissionAssignmentDto? ExistingAssignment { get; set; }

  [Inject]
  public required ILogger<PermissionAssignmentDialog> Logger { get; init; }

  [CascadingParameter]
  public required IMudDialogInstance MudDialog { get; init; }

  [Inject]
  public required IPermissionCatalogStore PermissionCatalogStore { get; init; }

  [Parameter]
  public required Guid PrincipalId { get; set; }

  [Parameter]
  public required PermissionPrincipalKind PrincipalKind { get; set; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  private bool IsEdit => ExistingAssignment is not null;

  /// <summary>
  /// True when the target principal is a server-kind service account.
  /// </summary>
  private bool PrincipalAllowsServerScope =>
    PrincipalKind == PermissionPrincipalKind.ServiceAccount &&
    AccountKind == ServiceAccountKind.Server;

  protected override async Task OnInitializedAsync()
  {
    if (PermissionCatalogStore.Items.Count == 0)
    {
      await PermissionCatalogStore.Refresh();
    }

    // Server-only permissions have no assignable scope without server authority, so hide them.
    _catalog = CanManageServerScope
      ? PermissionCatalogStore.Items
      : [.. PermissionCatalogStore.Items.Where(HasNonServerScope)];

    if (ExistingAssignment is { } existing)
    {
      _permissionName = existing.PermissionName;
      _selectedPermission = _catalog.FirstOrDefault(p => p.Name == existing.PermissionName);
      _effect = existing.Effect;
      _notes = existing.Notes ?? string.Empty;
      _isEnabled = existing.IsEnabled;

      if (_selectedPermission is null)
      {
        Snackbar.Add($"The permission '{existing.PermissionName}' no longer exists in the catalog.", Severity.Warning);
        _scopeKind = PermissionScopeKind.Tenant;
        _scopeId = null;
      }
      else if (!AvailableScopeKinds(_selectedPermission).Contains(existing.ScopeKind))
      {
        _scopeKind = BroadestAvailableScope(_selectedPermission);
        _scopeId = null;
        Snackbar.Add("The original scope kind is no longer valid for this permission and was reset.", Severity.Warning);
      }
      else
      {
        _scopeKind = existing.ScopeKind;
        _scopeId = existing.ScopeId;
      }
    }
  }

  private static bool HasNonServerScope(InternalDtos.PermissionCatalogEntryDto entry) =>
    entry.AllowedScopeKinds.Any(static kind => kind != PermissionScopeKind.Server);

  private static string ScopeKindLabel(PermissionScopeKind scopeKind) => scopeKind switch
  {
    PermissionScopeKind.Server => "Server",
    PermissionScopeKind.Tenant => "Tenant",
    PermissionScopeKind.Device => "Device",
    PermissionScopeKind.DeviceGroup => "Device Group",
    PermissionScopeKind.CustomerTenant => "Customer",
    _ => scopeKind.ToString()
  };

  /// <summary>
  /// Returns the scope kinds available in the dropdown for the selected permission.
  /// <see cref="PermissionScopeKind.Server"/> is hidden when the caller lacks
  /// <see cref="PermissionNames.ServerPermissionsWrite"/>, or when the permission is
  /// resource-scoped and the target is not a server-kind service account.
  /// </summary>
  private IReadOnlyList<PermissionScopeKind> AvailableScopeKinds(InternalDtos.PermissionCatalogEntryDto? entry)
  {
    if (entry is null)
    {
      return [];
    }

    if (CanManageServerScope && ServerScopeAllowedFor(entry))
    {
      return entry.AllowedScopeKinds;
    }

    return [.. entry.AllowedScopeKinds.Where(static kind => kind != PermissionScopeKind.Server)];
  }

  /// <summary>
  /// Returns the broadest legal scope available for the selected permission given the
  /// current principal, so the default selection never lands on a scope that is hidden.
  /// </summary>
  private PermissionScopeKind BroadestAvailableScope(InternalDtos.PermissionCatalogEntryDto? entry)
  {
    if (entry is null)
    {
      return PermissionScopeKind.Tenant;
    }

    var kinds = AvailableScopeKinds(entry);

    // Server is selectable for a deny but never pre-selected, so a tenant admin is never seeded
    // a server-wide row by default.
    if (!PrincipalAllowsServerScope)
    {
      kinds = [.. kinds.Where(static kind => kind != PermissionScopeKind.Server)];
    }

    return PermissionScopeKinds.GetBroadestLegalScope(kinds) ?? PermissionScopeKind.Tenant;
  }

  private void Cancel() => MudDialog.Cancel();

  private void HandleEffectChanged(PermissionEffect effect)
  {
    _effect = effect;

    if (_selectedPermission is { } entry && !AvailableScopeKinds(entry).Contains(_scopeKind))
    {
      _scopeKind = BroadestAvailableScope(entry);
      _scopeId = null;
    }
  }

  private void HandlePermissionChanged(InternalDtos.PermissionCatalogEntryDto? value)
  {
    _selectedPermission = value;
    _permissionName = value?.Name ?? string.Empty;
    _scopeKind = BroadestAvailableScope(value);
    _scopeId = null;
  }

  private async Task<IEnumerable<InternalDtos.PermissionCatalogEntryDto>> SearchPermissions(
    string query,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(query))
    {
      return _catalog;
    }

    await Task.CompletedTask;
    return _catalog.Where(p =>
      p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
      p.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase));
  }

  /// <summary>
  /// True when the Server scope should be shown for this permission and principal. Denies are
  /// exempt from the reach rule because a deny narrows only its own principal.
  /// </summary>
  private bool ServerScopeAllowedFor(InternalDtos.PermissionCatalogEntryDto entry) =>
    PrincipalAllowsServerScope
    || _effect == PermissionEffect.Deny
    || !entry.AllowedScopeKinds.Contains(PermissionScopeKind.Tenant);

  private async Task Submit()
  {
    if (string.IsNullOrWhiteSpace(_permissionName))
    {
      Snackbar.Add("Permission name is required", Severity.Error);
      return;
    }

    if ((_scopeKind is PermissionScopeKind.Device 
          or PermissionScopeKind.DeviceGroup 
          or PermissionScopeKind.CustomerTenant 
          or PermissionScopeKind.UserGroup) && _scopeId is null)
    {
      Snackbar.Add("Select a scope target", Severity.Error);
      return;
    }

    if (ExistingAssignment is { } existing)
    {
      try
      {
        var updateRequest = new InternalDtos.UpdatePermissionAssignmentRequestDto(
          _permissionName,
          _effect,
          _scopeKind,
          _scopeId,
          string.IsNullOrWhiteSpace(_notes) ? null : _notes,
          _isEnabled);

        var result = await ControlrApi.Internal.PermissionAssignments.Update(existing.Id, updateRequest);
        if (!result.IsSuccess)
        {
          Snackbar.Add(result.Reason, Severity.Error);
          return;
        }

        Snackbar.Add("Assignment updated", Severity.Success);
        MudDialog.Close(DialogResult.Ok(true));
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Failed to update permission assignment {AssignmentId}.", existing.Id);
        Snackbar.Add("Failed to update the assignment.", Severity.Error);
      }
      return;
    }

    try
    {
      var createRequest = new InternalDtos.CreatePermissionAssignmentRequestDto(
        PrincipalKind,
        PrincipalId,
        _permissionName,
        _effect,
        _scopeKind,
        _scopeId,
        string.IsNullOrWhiteSpace(_notes) ? null : _notes,
        _isEnabled);

      var createResult = await ControlrApi.Internal.PermissionAssignments.Create(createRequest);
      if (!createResult.IsSuccess)
      {
        Snackbar.Add(createResult.Reason, Severity.Error);
        return;
      }

      Snackbar.Add("Assignment created", Severity.Success);
      MudDialog.Close(DialogResult.Ok(true));
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Failed to create permission assignment for principal {PrincipalId}.", PrincipalId);
      Snackbar.Add("Failed to create the assignment.", Severity.Error);
    }
  }
}
