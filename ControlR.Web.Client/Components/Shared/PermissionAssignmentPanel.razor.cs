using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using PADtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

namespace ControlR.Web.Client.Components.Shared;

public partial class PermissionAssignmentPanel : ComponentBase
{
  private readonly HashSet<Guid> _togglingIds = [];

  private PADtos.PermissionAssignmentDto[]? _assignments;
  private bool _bulkDeleting;
  private bool _canManageServerScope;
  private Guid? _currentUserId;
  private bool _hasTenantWritePermission;
  private bool _hasWritePermission;
  private bool _loading;
  private PresetApplyMode _presetMode = PresetApplyMode.Merge;
  private PADtos.PermissionPresetDto[] _presets = [];
  private PermissionPrincipalKind _principalKind = PermissionPrincipalKind.User;
  private string _searchString = string.Empty;
  private HashSet<PADtos.PermissionAssignmentDto> _selectedAssignments = [];
  private IReadOnlyCollection<string> _selectedPresetNames = [];
  private Guid? _selectedPrincipalId;
  private Guid _tenantId;

  [Parameter]
  public ServiceAccountKind AccountKind { get; set; } = ServiceAccountKind.Tenant;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDialogService DialogService { get; init; }

  [Parameter]
  public bool IsPrincipalLocked { get; set; }

  [Inject]
  public required ILogger<PermissionAssignmentPanel> Logger { get; init; }

  [Inject]
  public required IPermissionCatalogStore PermissionCatalogStore { get; init; }

  [Parameter]
  public Guid? PrincipalId { get; set; }

  [Parameter]
  public PermissionPrincipalKind? PrincipalKind { get; set; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  private Func<PADtos.PermissionAssignmentDto, bool> QuickFilter => assignment =>
  {
    if (string.IsNullOrWhiteSpace(_searchString))
    {
      return true;
    }

    return assignment.PermissionName.Contains(_searchString, StringComparison.OrdinalIgnoreCase) ||
           assignment.Effect.ToString().Contains(_searchString, StringComparison.OrdinalIgnoreCase) ||
           assignment.ScopeKind.ToString().Contains(_searchString, StringComparison.OrdinalIgnoreCase);
  };

  protected override async Task OnInitializedAsync()
  {
    var state = await AuthState.GetAuthenticationStateAsync();
    if (state.User.TryGetUserId(out var currentUserId))
    {
      _currentUserId = currentUserId;
    }

    if (!state.User.TryGetTenantId(out var tenantId))
    {
      Snackbar.Add("No tenant is associated with the signed-in user.", Severity.Error);
      return;
    }

    _tenantId = tenantId;

    _hasTenantWritePermission = HasPolicy(state.User, PolicyNames.RequirePermissionAssignmentsWrite);
    _hasWritePermission = _hasTenantWritePermission || HasPolicy(state.User, PolicyNames.RequireServerPermissionsWrite);
    _canManageServerScope = HasPolicy(state.User, PolicyNames.RequireServerPermissionsWrite);

    if (PermissionCatalogStore.Items.Count == 0)
    {
      await PermissionCatalogStore.Refresh();
    }

    try
    {
      var presetsResult = await ControlrApi.V1.PermissionAssignments.GetPresets(_tenantId);
      if (presetsResult.IsSuccess)
      {
        _presets = [.. presetsResult.Value.Items];
      }
      else
      {
        Snackbar.Add(presetsResult.Reason, Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Failed to load permission presets.");
      Snackbar.Add("Failed to load permission presets.", Severity.Error);
    }

  }

  protected override void OnParametersSet()
  {
    if (IsPrincipalLocked && PrincipalKind is { } kind && PrincipalId is { } id)
    {
      _principalKind = kind;
      _selectedPrincipalId = id;
    }
  }

  protected override async Task OnParametersSetAsync()
  {
    await base.OnParametersSetAsync();

    if (IsPrincipalLocked && _selectedPrincipalId is not null && _assignments is null)
    {
      await LoadAssignments();
    }
  }

  private static bool HasPolicy(ClaimsPrincipal user, string policyName) =>
    user.HasClientPolicy(policyName);

  private async Task ApplyPresets()
  {
    if (_selectedPrincipalId is null || !_selectedPresetNames.Any())
    {
      return;
    }

    if (_presetMode == PresetApplyMode.Replace)
    {
      var confirmed = await DialogService.ShowMessageBoxAsync(
        "Replace Assignments",
        "This will replace all existing assignments with the selected preset permissions. Continue?",
        "Replace", "Cancel");

      if (!confirmed.GetValueOrDefault())
      {
        return;
      }
    }

    var result = await ControlrApi.V1.PermissionAssignments.ApplyPresets(
      _tenantId,
      new PADtos.ApplyPermissionPresetsRequestDto(
        _principalKind,
        _selectedPrincipalId.Value,
        [.. _selectedPresetNames],
        _presetMode == PresetApplyMode.Replace));
    if (!result.IsSuccess)
    {
      Snackbar.Add(result.Reason, Severity.Error);
      return;
    }

    Snackbar.Add($"Applied {result.Value} permission(s)", Severity.Success);
    await LoadAssignments();
  }

  private PermissionScopeKind BroadestLegalScope(string permissionName)
  {
    var entry = PermissionCatalogStore.Items.FirstOrDefault(p => p.Name == permissionName);
    return PermissionScopeKinds.GetBroadestTenantLegalScope(entry?.AllowedScopeKinds ?? []) ?? PermissionScopeKind.Tenant;
  }

  private async Task CreateAssignment()
  {
    if (!TryGetSelectedPrincipal(out var kind, out var principalId))
    {
      return;
    }

    var parameters = new DialogParameters<PermissionAssignmentDialog>
    {
      { x => x.PrincipalKind, kind },
      { x => x.PrincipalId, principalId },
      { x => x.AccountKind, AccountKind },
      { x => x.CanManageServerScope, _canManageServerScope },
      { x => x.TenantId, _tenantId }
    };

    var dialogOptions = PermissionAssignmentDialog.DefaultOptions;

    var dialog = await DialogService.ShowAsync<PermissionAssignmentDialog>("Create Assignment", parameters, dialogOptions);
    var result = await dialog.Result;

    if (result is not null && !result.Canceled)
    {
      await LoadAssignments();
    }
  }

  private async Task DeleteAssignment(PADtos.PermissionAssignmentDto assignment)
  {
    var confirmed = await DialogService.ShowMessageBoxAsync(
      "Delete Assignment",
      $"Delete the \"{assignment.PermissionName}\" ({assignment.Effect}) assignment?",
      "Delete", "Cancel");

    if (!confirmed.GetValueOrDefault())
    {
      return;
    }

    var result = await ControlrApi.V1.PermissionAssignments.Delete(assignment.Id, _tenantId);
    if (!result.IsSuccess)
    {
      Snackbar.Add(result.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("Assignment deleted", Severity.Success);
    await LoadAssignments();
  }

  private async Task DeleteSelectedAssignments()
  {
    if (_selectedAssignments.Count == 0 || _bulkDeleting)
    {
      return;
    }

    var selected = _selectedAssignments.ToList();

    var confirmed = await DialogService.ShowMessageBoxAsync(
      "Delete Assignments",
      $"Delete {selected.Count} assignment(s)?",
      "Delete", "Cancel");

    if (!confirmed.GetValueOrDefault())
    {
      return;
    }

    _bulkDeleting = true;

    try
    {
      var result = await ControlrApi.V1.PermissionAssignments.DeleteMany(
        _tenantId,
        new PADtos.DeleteManyPermissionAssignmentsRequestDto(
          [.. selected.Select(x => x.Id)]));

      if (!result.IsSuccess)
      {
        Snackbar.Add(result.Reason, Severity.Error);
        return;
      }

      _selectedAssignments.Clear();

      var successCount = result.Value.SuccessIds.Count;
      var failureCount = result.Value.FailureIds.Count;
      if (failureCount == 0)
      {
        Snackbar.Add($"Deleted {successCount} assignment(s)", Severity.Success);
      }
      else
      {
        Snackbar.Add($"Deleted {successCount} assignment(s); {failureCount} failed", Severity.Warning);
      }

      await LoadAssignments();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Failed to bulk-delete permission assignments.");
      Snackbar.Add("Failed to delete assignments.", Severity.Error);
    }
    finally
    {
      _bulkDeleting = false;
    }
  }

  private async Task EditAssignment(PADtos.PermissionAssignmentDto assignment)
  {
    var parameters = new DialogParameters<PermissionAssignmentDialog>
    {
      { x => x.ExistingAssignment, assignment },
      { x => x.PrincipalId, assignment.PrincipalId },
      { x => x.PrincipalKind, assignment.PrincipalKind },
      { x => x.AccountKind, AccountKind },
      { x => x.CanManageServerScope, _canManageServerScope },
      { x => x.TenantId, _tenantId }
    };

    var dialogOptions = PermissionAssignmentDialog.DefaultOptions;

    var dialog = await DialogService.ShowAsync<PermissionAssignmentDialog>("Edit Assignment", parameters, dialogOptions);
    var result = await dialog.Result;

    if (result is not null && !result.Canceled)
    {
      await LoadAssignments();
    }
  }

  /// <summary>
  /// True when the row is the current user's own last enabled Allow grant of a
  /// non-self-removable permission, so removing or disabling it would lock them out. Mirrors
  /// the server-side guard; the server remains authoritative.
  /// </summary>
  private bool IsProtectedSelfLastGrant(PADtos.PermissionAssignmentDto assignment)
  {
    if (_principalKind != PermissionPrincipalKind.User || _selectedPrincipalId != _currentUserId)
    {
      return false;
    }

    if (assignment.Effect != PermissionEffect.Allow || !assignment.IsEnabled)
    {
      return false;
    }

    var entry = PermissionCatalogStore.Items.FirstOrDefault(p => p.Name == assignment.PermissionName);
    if (entry is null || entry.SelfRemovable)
    {
      return false;
    }

    var grantCount = (_assignments ?? [])
      .Count(a => a.PermissionName == assignment.PermissionName &&
                  a.Effect == PermissionEffect.Allow &&
                  a.IsEnabled);

    return grantCount <= 1;
  }

  private async Task LoadAssignments()
  {
    if (_selectedPrincipalId is null)
    {
      return;
    }

    _loading = true;
    await InvokeAsync(StateHasChanged);

    try
    {
      var result = await ControlrApi.V1.PermissionAssignments.GetByPrincipal(
        _tenantId, _principalKind, _selectedPrincipalId.Value);
      if (result.IsSuccess)
      {
        _assignments = [.. result.Value.Items];
      }
      else
      {
        Snackbar.Add(result.Reason, Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Failed to load permission assignments for principal {PrincipalId}.", _selectedPrincipalId);
      Snackbar.Add("Failed to load assignments.", Severity.Error);
    }
    finally
    {
      _loading = false;
      await InvokeAsync(StateHasChanged);
    }
  }

  private async Task OnPresetSelectionChanged(IReadOnlyCollection<string> values)
  {
    _selectedPresetNames = values;
    await InvokeAsync(StateHasChanged);
  }

  private async Task OnPrincipalIdChanged(Guid? id)
  {
    if (IsPrincipalLocked)
    {
      return;
    }

    _selectedPrincipalId = id;
    _assignments = null;
    _selectedAssignments.Clear();

    if (id is not null)
    {
      await LoadAssignments();
    }
  }

  private async Task OnPrincipalKindChanged(PermissionPrincipalKind kind)
  {
    if (IsPrincipalLocked)
    {
      return;
    }

    _principalKind = kind;
    _selectedPrincipalId = null;
    _assignments = null;
    _selectedAssignments.Clear();
    await InvokeAsync(StateHasChanged);

    if (_selectedPrincipalId is not null)
    {
      await LoadAssignments();
    }
  }

  private async Task ShowNotes(string? notes)
  {
    if (string.IsNullOrWhiteSpace(notes))
    {
      return;
    }

    var dialogOptions = new DialogOptions
    {
      MaxWidth = MaxWidth.Medium,
      FullWidth = true
    };

    var parameters = new DialogParameters<NotesDialog>
    {
      { x => x.Notes, notes }
    };

    await DialogService.ShowAsync<NotesDialog>("Notes", parameters, dialogOptions);
  }

  private async Task ToggleEnabled(PADtos.PermissionAssignmentDto assignment, bool enabled)
  {
    if (_togglingIds.Contains(assignment.Id))
    {
      return;
    }

    _togglingIds.Add(assignment.Id);

    try
    {
      var updateRequest = new PADtos.UpdatePermissionAssignmentRequestDto(
        assignment.PermissionName,
        assignment.Effect,
        assignment.ScopeKind,
        assignment.ScopeId,
        assignment.Notes,
        enabled);

      var result = await ControlrApi.V1.PermissionAssignments.Update(assignment.Id, _tenantId, updateRequest);
      if (!result.IsSuccess)
      {
        Snackbar.Add(result.Reason, Severity.Error);
        return;
      }

      if (_assignments is not null)
      {
        var index = Array.IndexOf(_assignments, assignment);
        if (index >= 0)
        {
          var updated = _assignments[index] with { IsEnabled = enabled };
          _assignments = [.. _assignments[..index], updated, .. _assignments[(index + 1)..]];
        }
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Failed to toggle permission assignment {AssignmentId}.", assignment.Id);
      Snackbar.Add("Failed to update assignment.", Severity.Error);
    }
    finally
    {
      _togglingIds.Remove(assignment.Id);
    }
  }

  private bool TryGetSelectedPrincipal(out PermissionPrincipalKind kind, out Guid principalId)
  {
    if (_selectedPrincipalId is null)
    {
      Snackbar.Add("Select a principal first", Severity.Error);
      kind = default;
      principalId = default;
      return false;
    }

    kind = _principalKind;
    principalId = _selectedPrincipalId.Value;
    return true;
  }

  private enum PresetApplyMode
  {
    Merge,
    Replace
  }

}
