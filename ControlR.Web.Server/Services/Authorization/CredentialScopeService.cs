using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Primitives;

namespace ControlR.Web.Server.Services.Authorization;

/// <summary>
/// Grant-authority operations for credential scopes: validates that a creator may grant the
/// requested scopes (the evaluator is invoked per scope, so deny overrides and group/customer
/// membership precision apply), and writes logon-token scope grant rows with change-log
/// entries. See the permission semantics spec, sections 3 and 6.
/// </summary>
public interface ICredentialScopeService
{
  Task<HttpResult> ValidateGrantableScopes(
    PrincipalDescriptor creator,
    Guid tenantId,
    IReadOnlyList<InternalDtos.CredentialScopeDto> scopes,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Validates a logon token's scopes; non-Device scopes are rejected.
  /// </summary>
  Task<HttpResult> ValidateLogonTokenScopes(
    PrincipalDescriptor creator,
    Guid tenantId,
    IReadOnlyList<InternalDtos.CredentialScopeDto> scopes,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Writes a logon token's device-scoped grants and a change-log entry. The token is
  /// created without baseline grants when explicit scopes are supplied, so these are its
  /// full permission set.
  /// </summary>
  Task WriteLogonTokenScopes(
    Guid tokenId,
    Guid deviceId,
    Guid tenantId,
    PrincipalDescriptor actor,
    IReadOnlyList<InternalDtos.CredentialScopeDto> scopes,
    CancellationToken cancellationToken = default);
}

public class CredentialScopeService(
  AppDb appDb,
  IAuthorizationChangeLogFactory changeLogFactory,
  IPermissionEvaluator permissionEvaluator,
  IResourceDescriptorFactory resourceFactory) : ICredentialScopeService
{
  private readonly AppDb _appDb = appDb;
  private readonly IAuthorizationChangeLogFactory _changeLogFactory = changeLogFactory;
  private readonly IPermissionEvaluator _permissionEvaluator = permissionEvaluator;
  private readonly IResourceDescriptorFactory _resourceFactory = resourceFactory;

  public async Task<HttpResult> ValidateGrantableScopes(
    PrincipalDescriptor creator,
    Guid tenantId,
    IReadOnlyList<InternalDtos.CredentialScopeDto> scopes,
    CancellationToken cancellationToken = default)
  {
    var scopeResources = await ResolveScopes(scopes, tenantId, cancellationToken);

    var requests = new List<PermissionEvaluationRequest>(scopes.Count);
    for (var i = 0; i < scopes.Count; i++)
    {
      var scopeResource = scopeResources[i];
      if (scopeResource is null)
      {
        return HttpResult.Fail(
          HttpResultErrorCode.BadRequest,
          $"Scope target not found in this tenant: {scopes[i].ScopeKind}/{scopes[i].ScopeId}.");
      }

      // A credential is always tenant-bound, so a Server-scope grant of a tenant-addressable
      // permission would reach outside its tenant even when the creator holds that reach.
      if (scopes[i].ScopeKind == PermissionScopeKind.Server &&
          PermissionCatalog.AllowsTenantScope(scopes[i].PermissionName))
      {
        return HttpResult.Fail(
          HttpResultErrorCode.BadRequest,
          $"'{scopes[i].PermissionName}' cannot be granted at Server scope to a credential. " +
          "Server-scoped resource permissions are reserved for server service accounts.");
      }

      requests.Add(new PermissionEvaluationRequest(scopes[i].PermissionName, scopeResource));
    }

    var decisions = await _permissionEvaluator.EvaluateBatch(
      creator,
      requests,
      cancellationToken);
    for (var index = 0; index < decisions.Count; index++)
    {
      if (!decisions[requests[index]].Allowed)
      {
        var scope = scopes[index];
        return HttpResult.Fail(
          HttpResultErrorCode.BadRequest,
          $"The permission '{scope.PermissionName}' at {scope.ScopeKind} scope is outside the effective permissions of the granting user.");
      }
    }

    return HttpResult.Ok();
  }

  public async Task<HttpResult> ValidateLogonTokenScopes(
    PrincipalDescriptor creator,
    Guid tenantId,
    IReadOnlyList<InternalDtos.CredentialScopeDto> scopes,
    CancellationToken cancellationToken = default)
  {
    foreach (var scope in scopes)
    {
      if (scope.ScopeKind != PermissionScopeKind.Device)
      {
        return HttpResult.Fail(
          HttpResultErrorCode.BadRequest,
          $"Logon token scopes must be Device-scoped; '{scope.ScopeKind}' scopes are not honored for logon tokens.");
      }
    }

    return await ValidateGrantableScopes(creator, tenantId, scopes, cancellationToken);
  }

  public async Task WriteLogonTokenScopes(
    Guid tokenId,
    Guid deviceId,
    Guid tenantId,
    PrincipalDescriptor actor,
    IReadOnlyList<InternalDtos.CredentialScopeDto> scopes,
    CancellationToken cancellationToken = default)
  {
    foreach (var scope in scopes)
    {
      // ValidateLogonTokenScopes guarantees ScopeId == deviceId; fail loudly on deviation.
      var scopeDeviceId = scope.ScopeId ??
        throw new InvalidOperationException("Logon token scope is missing its device id. Scopes must be validated before writing.");
      if (scopeDeviceId != deviceId)
      {
        throw new InvalidOperationException("Logon token scope targets a device other than the token's device. Scopes must be validated before writing.");
      }

      _appDb.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
        PermissionPrincipalKind.LogonToken,
        tokenId,
        scope.PermissionName,
        scope.ScopeKind,
        scopeDeviceId,
        tenantId,
        actor));
    }

    _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.CredentialScopeSet,
      actor,
      AuthorizationChangeLogTargetTypes.LogonToken,
      tokenId,
      tenantId,
      after: new CredentialScopeSetSummary(scopes.Count)));

    await _appDb.SaveChangesAsync(cancellationToken);
  }

  /// <summary>
  /// Resolves every requested scope in a bounded number of queries by grouping the lookups
  /// by scope kind, instead of issuing one <see cref="IResourceDescriptorFactory.CreateScope"/>
  /// (and one DbContext/query) per scope.
  /// </summary>
  private async Task<IReadOnlyList<ResourceDescriptor?>> ResolveScopes(
    IReadOnlyList<InternalDtos.CredentialScopeDto> scopes,
    Guid tenantId,
    CancellationToken cancellationToken)
  {
    var results = new ResourceDescriptor?[scopes.Count];

    var deviceGroupIds = scopes
      .Where(s => s.ScopeKind == PermissionScopeKind.DeviceGroup && s.ScopeId.HasValue)
      .Select(s => s.ScopeId!.Value)
      .Distinct()
      .ToList();
    var customerIds = scopes
      .Where(s => s.ScopeKind == PermissionScopeKind.CustomerTenant && s.ScopeId.HasValue)
      .Select(s => s.ScopeId!.Value)
      .Distinct()
      .ToList();
    var userGroupIds = scopes
      .Where(s => s.ScopeKind == PermissionScopeKind.UserGroup && s.ScopeId.HasValue)
      .Select(s => s.ScopeId!.Value)
      .Distinct()
      .ToList();
    var deviceIds = scopes
      .Where(s => s.ScopeKind == PermissionScopeKind.Device && s.ScopeId.HasValue)
      .Select(s => s.ScopeId!.Value)
      .Distinct()
      .ToList();

    var deviceGroupSet = deviceGroupIds.Count == 0
      ? []
      : (await _appDb.DeviceGroups
          .IgnoreQueryFilters()
          .AsNoTracking()
          .Where(g => deviceGroupIds.Contains(g.Id) && g.TenantId == tenantId)
          .Select(g => g.Id)
          .ToListAsync(cancellationToken))
        .ToHashSet();

    var customerSet = customerIds.Count == 0
      ? []
      : (await _appDb.Customers
          .IgnoreQueryFilters()
          .AsNoTracking()
          .Where(c => customerIds.Contains(c.Id) && c.TenantId == tenantId)
          .Select(c => c.Id)
          .ToListAsync(cancellationToken))
        .ToHashSet();

    var userGroupSet = userGroupIds.Count == 0
      ? []
      : (await _appDb.UserGroups
          .IgnoreQueryFilters()
          .AsNoTracking()
          .Where(g => userGroupIds.Contains(g.Id) && g.TenantId == tenantId)
          .Select(g => g.Id)
          .ToListAsync(cancellationToken))
        .ToHashSet();

    var deviceLookup = deviceIds.Count == 0
      ? new Dictionary<Guid, Device>()
      : (await _appDb.Devices
          .IgnoreQueryFilters()
          .AsNoTracking()
          .Include(x => x.DeviceGroupMembers)
          .Where(d => deviceIds.Contains(d.Id) && d.TenantId == tenantId)
          .ToListAsync(cancellationToken))
        .ToDictionary(d => d.Id);

    for (var i = 0; i < scopes.Count; i++)
    {
      var scope = scopes[i];
      results[i] = scope.ScopeKind switch
      {
        PermissionScopeKind.Server => _resourceFactory.CreateServer(),
        PermissionScopeKind.Tenant =>
          scope.ScopeId is null || scope.ScopeId == tenantId
            ? _resourceFactory.CreateTenant(tenantId)
            : null,
        PermissionScopeKind.DeviceGroup =>
          scope.ScopeId.HasValue && deviceGroupSet.Contains(scope.ScopeId.Value)
            ? new ResourceDescriptor(PermissionScopeKind.DeviceGroup, scope.ScopeId, tenantId)
            : null,
        PermissionScopeKind.CustomerTenant =>
          scope.ScopeId.HasValue && customerSet.Contains(scope.ScopeId.Value)
            ? new ResourceDescriptor(PermissionScopeKind.CustomerTenant, scope.ScopeId, tenantId)
            : null,
        PermissionScopeKind.UserGroup =>
          scope.ScopeId.HasValue && userGroupSet.Contains(scope.ScopeId.Value)
            ? new ResourceDescriptor(PermissionScopeKind.UserGroup, scope.ScopeId, tenantId)
            : null,
        PermissionScopeKind.Device =>
          scope.ScopeId.HasValue && deviceLookup.TryGetValue(scope.ScopeId.Value, out var device)
            ? await _resourceFactory.CreateDevice(device, cancellationToken)
            : null,
        _ => null
      };
    }

    return results;
  }

}
