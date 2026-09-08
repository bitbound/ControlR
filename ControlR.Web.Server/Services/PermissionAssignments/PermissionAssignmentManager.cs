using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.Authorization.PermissionRules;
using ControlR.Web.Server.Services.Locks;

namespace ControlR.Web.Server.Services.PermissionAssignments;

/// <summary>
/// Manages permission assignments: CRUD with validation, cleanup on principal/resource
/// deletion, and effective permission queries. All writes emit AuthorizationChangeLog entries.
/// </summary>
public interface IPermissionAssignmentManager
{
  Task<HttpResult<int>> ApplyPresets(
    InternalDtos.ApplyPermissionPresetsRequestDto request,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken = default);
  Task<HttpResult<InternalDtos.PermissionAssignmentDto>> Create(
    InternalDtos.CreatePermissionAssignmentRequestDto request,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken = default);
  Task<HttpResult> CreateMany(
    IReadOnlyList<InternalDtos.CreatePermissionAssignmentRequestDto> requests,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken = default);
  Task<HttpResult> Delete(
    Guid assignmentId,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken = default);
  Task<HttpResult<InternalDtos.DeleteManyPermissionAssignmentsResponseDto>> DeleteMany(
    IReadOnlyList<Guid> assignmentIds,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken = default);
  Task<IReadOnlyList<InternalDtos.PermissionAssignmentDto>> GetByPrincipal(
    PermissionPrincipalKind principalKind,
    Guid principalId,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Replaces a principal's assignments for the given scope kinds (change-logged).
  /// </summary>
  Task<HttpResult> ReplaceForPrincipal(
    PermissionPrincipalKind principalKind,
    Guid principalId,
    Guid tenantId,
    PrincipalDescriptor actor,
    IReadOnlyList<InternalDtos.CreatePermissionAssignmentRequestDto> assignments,
    CancellationToken cancellationToken = default);
  Task<HttpResult<InternalDtos.PermissionAssignmentDto>> Update(
    Guid assignmentId,
    InternalDtos.UpdatePermissionAssignmentRequestDto request,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken = default);
}

public class PermissionAssignmentManager(
  AppDb appDb,
  IPermissionDecisionEvaluator decisionEvaluator,
  IPermissionEvaluator permissionEvaluator,
  ICredentialScopeService credentialScopeService,
  IAuthorizationChangeLogFactory changeLogFactory,
  IAsyncLock asyncLock) : IPermissionAssignmentManager
{
  private readonly AppDb _appDb = appDb;
  private readonly IAsyncLock _asyncLock = asyncLock;
  private readonly IAuthorizationChangeLogFactory _changeLogFactory = changeLogFactory;
  private readonly ICredentialScopeService _credentialScopeService = credentialScopeService;
  private readonly IPermissionDecisionEvaluator _decisionEvaluator = decisionEvaluator;
  private readonly IPermissionEvaluator _permissionEvaluator = permissionEvaluator;

  public async Task<HttpResult<int>> ApplyPresets(
    InternalDtos.ApplyPermissionPresetsRequestDto request,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken = default)
  {
    var permissionNames = request.PresetNames
      .SelectMany(PermissionPresets.GetPermissions)
      .Distinct()
      .ToList();

    if (permissionNames.Count == 0)
    {
      return HttpResult.Fail<int>(HttpResultErrorCode.BadRequest, "No preset permissions were found.");
    }

    var assignments = permissionNames
      .Select(permissionName => new InternalDtos.CreatePermissionAssignmentRequestDto(
        request.PrincipalKind,
        request.PrincipalId,
        permissionName,
        PermissionEffect.Allow,
        PermissionCatalog.GetBroadestTenantLegalScope(permissionName) ?? PermissionScopeKind.Tenant,
        null,
        null))
      .ToList();

    if (!request.ReplaceExisting)
    {
      var existingKeys = await _appDb.PermissionAssignments
        .IgnoreQueryFilters()
        .Where(x => x.PrincipalKind == request.PrincipalKind &&
                    x.PrincipalId == request.PrincipalId &&
                    x.OwningTenantId == tenantId)
        .Select(x => new { x.PermissionName, x.ScopeKind })
        .ToListAsync(cancellationToken);

      var existingKeySet = existingKeys
        .Select(x => (x.PermissionName, x.ScopeKind))
        .ToHashSet();

      assignments = assignments
        .Where(x => !existingKeySet.Contains((x.PermissionName, x.ScopeKind)))
        .ToList();

      if (assignments.Count == 0)
      {
        return HttpResult.Ok(0);
      }
    }

    async Task<HttpResult<int>> Apply()
    {
      HttpResult result;
      if (request.ReplaceExisting)
      {
        result = await ReplaceForPrincipal(
          request.PrincipalKind,
          request.PrincipalId,
          tenantId,
          actor,
          assignments,
          cancellationToken);
      }
      else
      {
        result = await CreateMany(assignments, tenantId, actor, cancellationToken);
      }

      return result.IsSuccess
        ? HttpResult.Ok(assignments.Count)
        : HttpResult.Fail<int>(result.ErrorCode, result.Reason);
    }

    return await Apply();
  }

  public async Task<HttpResult<InternalDtos.PermissionAssignmentDto>> Create(
    InternalDtos.CreatePermissionAssignmentRequestDto request,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken = default)
  {
    var principalExists = await ValidatePrincipalExists(
      request.PrincipalKind, request.PrincipalId, tenantId, cancellationToken);
    if (!principalExists)
    {
      return HttpResult.Fail<InternalDtos.PermissionAssignmentDto>(
        HttpResultErrorCode.BadRequest, $"Principal not found: {request.PrincipalKind}/{request.PrincipalId}");
    }

    var effectivePermissions = await GetEffectivePermissions(actor, tenantId, cancellationToken);
    if (ValidateWriteAuthority(request.Effect, request.ScopeKind, effectivePermissions) is { } authorityError)
    {
      return HttpResult.Fail<InternalDtos.PermissionAssignmentDto>(
      authorityError.Code, authorityError.Reason);
    }

    var targetIsServerServiceAccount = await IsServerServiceAccount(request.PrincipalKind, request.PrincipalId, cancellationToken);

    if (ValidateServerServiceAccountTarget(targetIsServerServiceAccount, effectivePermissions) is { IsSuccess: false } targetError)
    {
      return HttpResult.Fail<InternalDtos.PermissionAssignmentDto>(targetError.ErrorCode, targetError.Reason);
    }

    if (await ValidatePermissionScope(request.PermissionName, request.ScopeKind, request.ScopeId, request.Effect, tenantId,
      targetIsServerServiceAccount, cancellationToken) is { } scopeError)
    {
      return HttpResult.Fail<InternalDtos.PermissionAssignmentDto>(scopeError.Code, scopeError.Reason);
    }

    if (await ValidateCredentialPrincipalScope(
      request.PrincipalKind, request.PrincipalId, request.PermissionName,
      request.ScopeKind, request.ScopeId, request.Effect, cancellationToken) is { } credentialScopeError)
    {
      return HttpResult.Fail<InternalDtos.PermissionAssignmentDto>(HttpResultErrorCode.BadRequest, credentialScopeError);
    }

    var normalizedScopeId = NormalizeScopeId(request.ScopeKind, request.ScopeId, tenantId);
    if (await AssignmentExists(
      request.PrincipalKind,
      request.PrincipalId,
      request.PermissionName,
      request.Effect,
      request.ScopeKind,
      normalizedScopeId,
      cancellationToken))
    {
      return HttpResult.Fail<InternalDtos.PermissionAssignmentDto>(
        HttpResultErrorCode.Conflict, "An identical permission assignment already exists.");
    }

    var assignment = PermissionAssignment.CreateGrant(
      request.PrincipalKind,
      request.PrincipalId,
      request.PermissionName,
      request.ScopeKind,
      normalizedScopeId,
      tenantId,
      actor,
      request.Effect,
      request.Notes,
      request.IsEnabled);

    if (request.PrincipalKind == PermissionPrincipalKind.User &&
        request.PrincipalId == actor.PrincipalId &&
        await FindViolatedSelfProtected(
          actor,
          tenantId,
          direct => [.. direct, assignment],
          cancellationToken) is { } createViolation)
    {
      return HttpResult.Fail<InternalDtos.PermissionAssignmentDto>(
        HttpResultErrorCode.BadRequest,
        createViolation);
    }

    try
    {
      await _appDb.ExecuteInTransaction(async () =>
      {
        _appDb.PermissionAssignments.Add(assignment);
        await _appDb.SaveChangesAsync(cancellationToken);

        _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
          AuthorizationChangeLogActions.PermissionAssignmentCreated,
          actor,
          AuthorizationChangeLogTargetTypes.PermissionAssignment,
          assignment.Id,
          assignment.OwningTenantId,
          after: new PermissionAssignmentSnapshot(
            request.PermissionName, request.Effect, request.ScopeKind, request.ScopeId)));

        await _appDb.SaveChangesAsync(cancellationToken);
      }, cancellationToken);
    }
    catch (DbUpdateException)
    {
      if (await AssignmentExists(
        request.PrincipalKind,
        request.PrincipalId,
        request.PermissionName,
        request.Effect,
        request.ScopeKind,
        normalizedScopeId,
        cancellationToken))
      {
        return HttpResult.Fail<InternalDtos.PermissionAssignmentDto>(
          HttpResultErrorCode.Conflict, "An identical permission assignment already exists.");
      }

      throw;
    }

    return HttpResult.Ok(MapToDto(assignment));
  }

  public async Task<HttpResult> CreateMany(
    IReadOnlyList<InternalDtos.CreatePermissionAssignmentRequestDto> requests,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken = default)
  {
    if (requests.Count == 0)
    {
      return HttpResult.Fail(HttpResultErrorCode.BadRequest, "No assignments were provided.");
    }

    if (requests.Any(request => request.PrincipalKind != requests[0].PrincipalKind || request.PrincipalId != requests[0].PrincipalId))
    {
      return HttpResult.Fail(HttpResultErrorCode.BadRequest, "All assignments must target the same principal.");
    }

    var requestKeys = requests
      .Select(request => new AssignmentKey(
        request.PermissionName,
        request.Effect,
        request.ScopeKind,
        NormalizeScopeId(request.ScopeKind, request.ScopeId, tenantId)))
      .ToList();

    if (requestKeys.Count != requestKeys.Distinct().Count())
    {
      return HttpResult.Fail(HttpResultErrorCode.Conflict, "The request contains duplicate permission assignments.");
    }

    var principalExists = await ValidatePrincipalExists(
      requests[0].PrincipalKind, requests[0].PrincipalId, tenantId, cancellationToken);
    if (!principalExists)
    {
      return HttpResult.Fail(
        HttpResultErrorCode.BadRequest, $"Principal not found: {requests[0].PrincipalKind}/{requests[0].PrincipalId}");
    }

    var effectivePermissions = await GetEffectivePermissions(actor, tenantId, cancellationToken);
    foreach (var request in requests)
    {
      if (ValidateWriteAuthority(request.Effect, request.ScopeKind, effectivePermissions) is { } authorityError)
      {
        return HttpResult.Fail(authorityError.Code, authorityError.Reason);
      }
    }

    var targetIsServerServiceAccount = await IsServerServiceAccount(requests[0].PrincipalKind, requests[0].PrincipalId, cancellationToken);

    if (ValidateServerServiceAccountTarget(targetIsServerServiceAccount, effectivePermissions) is { IsSuccess: false } targetError)
    {
      return HttpResult.Fail(targetError.ErrorCode, targetError.Reason);
    }

    foreach (var request in requests)
    {
      var requestKey = new AssignmentKey(
        request.PermissionName,
        request.Effect,
        request.ScopeKind,
        NormalizeScopeId(request.ScopeKind, request.ScopeId, tenantId));
      if (await AssignmentExists(request.PrincipalKind, request.PrincipalId, requestKey, cancellationToken))
      {
        return HttpResult.Fail(HttpResultErrorCode.Conflict, "An identical permission assignment already exists.");
      }
    }

    var created = new List<PermissionAssignment>(requests.Count);

    // Validate all requests before staging any entities.
    foreach (var request in requests)
    {
      if (await ValidatePermissionScope(request.PermissionName, request.ScopeKind, request.ScopeId, request.Effect, tenantId,
        targetIsServerServiceAccount, cancellationToken) is { } scopeError)
      {
        return HttpResult.Fail(scopeError.Code, scopeError.Reason);
      }

      if (await ValidateCredentialPrincipalScope(
        request.PrincipalKind, request.PrincipalId, request.PermissionName,
        request.ScopeKind, request.ScopeId, request.Effect, cancellationToken) is { } credentialScopeError)
      {
        return HttpResult.Fail(HttpResultErrorCode.BadRequest, credentialScopeError);
      }
    }

    foreach (var request in requests)
    {
      var assignment = PermissionAssignment.CreateGrant(
        request.PrincipalKind,
        request.PrincipalId,
        request.PermissionName,
        request.ScopeKind,
        NormalizeScopeId(request.ScopeKind, request.ScopeId, tenantId),
        tenantId,
        actor,
        request.Effect,
        request.Notes,
        request.IsEnabled);

      created.Add(assignment);
    }

    if (requests[0].PrincipalKind == PermissionPrincipalKind.User &&
        requests[0].PrincipalId == actor.PrincipalId &&
        await FindViolatedSelfProtected(
          actor,
          tenantId,
          direct => [.. direct, .. created],
          cancellationToken) is { } createManyViolation)
    {
      return HttpResult.Fail(HttpResultErrorCode.BadRequest, createManyViolation);
    }

    _appDb.PermissionAssignments.AddRange(created);

    try
    {
      await _appDb.ExecuteInTransaction(async () =>
      {
        await _appDb.SaveChangesAsync(cancellationToken);

        for (var i = 0; i < requests.Count; i++)
        {
          var request = requests[i];
          var assignment = created[i];
          _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
            AuthorizationChangeLogActions.PermissionAssignmentCreated,
            actor,
            AuthorizationChangeLogTargetTypes.PermissionAssignment,
            assignment.Id,
            assignment.OwningTenantId,
            after: new PermissionAssignmentSnapshot(
              request.PermissionName, request.Effect, request.ScopeKind, request.ScopeId)));
        }

        await _appDb.SaveChangesAsync(cancellationToken);
      }, cancellationToken);
    }
    catch (DbUpdateException)
    {
      foreach (var request in requests)
      {
        if (await AssignmentExists(
          request.PrincipalKind,
          request.PrincipalId,
          request.PermissionName,
          request.Effect,
          request.ScopeKind,
          NormalizeScopeId(request.ScopeKind, request.ScopeId, tenantId),
          cancellationToken))
        {
          return HttpResult.Fail(HttpResultErrorCode.Conflict, "An identical permission assignment already exists.");
        }
      }

      throw;
    }

    return HttpResult.Ok();
  }

  public async Task<HttpResult> Delete(
    Guid assignmentId,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken = default)
  {
    var assignment = await _appDb.PermissionAssignments
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == assignmentId, cancellationToken);

    if (assignment is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "Permission assignment not found.");
    }

    var effectivePermissions = await GetEffectivePermissions(actor, tenantId, cancellationToken);
    if (!IsVisibleToTenant(assignment, tenantId, effectivePermissions))
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "Permission assignment not found.");
    }

    var targetIsServerServiceAccount = await IsServerServiceAccount(assignment.PrincipalKind, assignment.PrincipalId, cancellationToken);

    if (ValidateServerServiceAccountTarget(targetIsServerServiceAccount, effectivePermissions) is { IsSuccess: false } targetError)
    {
      return HttpResult.Fail(targetError.ErrorCode, targetError.Reason);
    }

    if (ValidateWriteAuthority(assignment.Effect, assignment.ScopeKind, effectivePermissions) is { } authorityError)
    {
      return HttpResult.Fail(authorityError.Code, authorityError.Reason);
    }

    if (IsSelf(assignment, actor.PrincipalId))
    {
      if (await FindViolatedSelfProtected(
        actor,
        tenantId,
        direct => [.. direct.Where(row => row.Id != assignmentId)],
        cancellationToken) is { } violation)
      {
        return HttpResult.Fail(HttpResultErrorCode.BadRequest, violation);
      }
    }

    _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.PermissionAssignmentDeleted,
      actor,
      AuthorizationChangeLogTargetTypes.PermissionAssignment,
      assignmentId,
      assignment.OwningTenantId,
      before: new PermissionAssignmentSnapshot(
        assignment.PermissionName, assignment.Effect, assignment.ScopeKind, assignment.ScopeId)));

    _appDb.PermissionAssignments.Remove(assignment);
    await _appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok();
  }

  public async Task<HttpResult<InternalDtos.DeleteManyPermissionAssignmentsResponseDto>> DeleteMany(
    IReadOnlyList<Guid> assignmentIds,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken = default)
  {
    if (assignmentIds.Count == 0)
    {
      return HttpResult.Fail<InternalDtos.DeleteManyPermissionAssignmentsResponseDto>(
        HttpResultErrorCode.BadRequest, "No assignment IDs were provided.");
    }

    var foundAssignments = await _appDb.PermissionAssignments
      .IgnoreQueryFilters()
      .Where(x => assignmentIds.Contains(x.Id))
      .ToListAsync(cancellationToken);

    var effectivePermissions = await GetEffectivePermissions(actor, tenantId, cancellationToken);

    var assignments = foundAssignments
      .Where(x => IsVisibleToTenant(x, tenantId, effectivePermissions))
      .ToList();

    var serverAccountPrincipals = await GetServerServiceAccountPrincipals(
      assignments.Select(x => (x.PrincipalKind, x.PrincipalId)),
      cancellationToken);

    foreach (var assignment in assignments)
    {
      if (ValidateServerServiceAccountTarget(serverAccountPrincipals.Contains((assignment.PrincipalKind, assignment.PrincipalId)), effectivePermissions) is { IsSuccess: false } targetError)
      {
        return HttpResult.Fail<InternalDtos.DeleteManyPermissionAssignmentsResponseDto>(targetError.ErrorCode, targetError.Reason);
      }

      if (ValidateWriteAuthority(assignment.Effect, assignment.ScopeKind, effectivePermissions) is { } authorityError)
      {
        return HttpResult.Fail<InternalDtos.DeleteManyPermissionAssignmentsResponseDto>(authorityError.Code, authorityError.Reason);
      }
    }

    var foundIds = assignments.Select(x => x.Id).ToHashSet();
    var successIds = new List<Guid>(assignments.Count);
    var failureIds = assignmentIds.Except(foundIds).ToList();

    if (assignments.Any(x => IsSelf(x, actor.PrincipalId)))
    {
      if (await FindViolatedSelfProtected(
        actor,
        tenantId,
        direct => [.. direct.Where(row => !assignmentIds.Contains(row.Id))],
        cancellationToken) is { } violation)
      {
        return HttpResult.Fail<InternalDtos.DeleteManyPermissionAssignmentsResponseDto>(
          HttpResultErrorCode.BadRequest, violation);
      }
    }

    foreach (var assignment in assignments)
    {
      _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
        AuthorizationChangeLogActions.PermissionAssignmentDeleted,
        actor,
        AuthorizationChangeLogTargetTypes.PermissionAssignment,
        assignment.Id,
        assignment.OwningTenantId,
        before: new PermissionAssignmentSnapshot(
          assignment.PermissionName, assignment.Effect, assignment.ScopeKind, assignment.ScopeId)));

      _appDb.PermissionAssignments.Remove(assignment);
      successIds.Add(assignment.Id);
    }

    await _appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok(new InternalDtos.DeleteManyPermissionAssignmentsResponseDto(successIds, failureIds));
  }

  public async Task<IReadOnlyList<InternalDtos.PermissionAssignmentDto>> GetByPrincipal(
    PermissionPrincipalKind principalKind,
    Guid principalId,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken = default)
  {
    var effectivePermissions = await GetEffectivePermissions(actor, tenantId, cancellationToken);

    var assignments = await _appDb.PermissionAssignments
      .IgnoreQueryFilters()
      .Where(x => x.PrincipalKind == principalKind &&
                  x.PrincipalId == principalId &&
                  (x.OwningTenantId == tenantId ||
                  (x.OwningTenantId == null && effectivePermissions.Contains(PermissionNames.ServerPermissionsRead))))
      .OrderBy(x => x.PermissionName)
      .ThenBy(x => x.ScopeKind)
      .ToListAsync(cancellationToken);

    return [.. assignments.Select(MapToDto)];
  }

  public async Task<HttpResult> ReplaceForPrincipal(
    PermissionPrincipalKind principalKind,
    Guid principalId,
    Guid tenantId,
    PrincipalDescriptor actor,
    IReadOnlyList<InternalDtos.CreatePermissionAssignmentRequestDto> assignments,
    CancellationToken cancellationToken = default)
  {
    var principalExists = await ValidatePrincipalExists(principalKind, principalId, tenantId, cancellationToken);
    if (!principalExists)
    {
      return HttpResult.Fail(
        HttpResultErrorCode.BadRequest, $"Principal not found: {principalKind}/{principalId}");
    }

    var effectivePermissions = await GetEffectivePermissions(actor, tenantId, cancellationToken);
    var replacedScopeKinds = assignments
      .Select(x => x.ScopeKind)
      .ToHashSet();

    foreach (var request in assignments)
    {
      if (ValidateWriteAuthority(request.Effect, request.ScopeKind, effectivePermissions) is { } authorityError)
      {
        return HttpResult.Fail(authorityError.Code, authorityError.Reason);
      }
    }

    var targetIsServerServiceAccount = await IsServerServiceAccount(principalKind, principalId, cancellationToken);

    if (ValidateServerServiceAccountTarget(targetIsServerServiceAccount, effectivePermissions) is { IsSuccess: false } targetError)
    {
      return HttpResult.Fail(targetError.ErrorCode, targetError.Reason);
    }

    if (principalKind == PermissionPrincipalKind.User && principalId == actor.PrincipalId)
    {
      var replacementRows = assignments
        .Select(request => PermissionAssignment.CreateGrant(
          principalKind,
          principalId,
          request.PermissionName,
          request.ScopeKind,
          NormalizeScopeId(request.ScopeKind, request.ScopeId, tenantId),
          tenantId,
          actor,
          request.Effect,
          request.Notes,
          request.IsEnabled))
        .ToList();
      if (await FindViolatedSelfProtected(
        actor,
        tenantId,
        direct => [
          .. direct.Where(row => !replacedScopeKinds.Contains(row.ScopeKind)),
          .. replacementRows
        ],
        cancellationToken) is { } violation)
      {
        return HttpResult.Fail(HttpResultErrorCode.BadRequest, violation);
      }
    }

    // Validate all requests before staging any entities or taking the lock.
    foreach (var request in assignments)
    {
      if (await ValidatePermissionScope(request.PermissionName, request.ScopeKind, request.ScopeId, request.Effect, tenantId,
        targetIsServerServiceAccount, cancellationToken) is { } scopeError)
      {
        return HttpResult.Fail(scopeError.Code, scopeError.Reason);
      }

      if (await ValidateCredentialPrincipalScope(
        principalKind, principalId, request.PermissionName,
        request.ScopeKind, request.ScopeId, request.Effect, cancellationToken) is { } credentialScopeError)
      {
        return HttpResult.Fail(HttpResultErrorCode.BadRequest, credentialScopeError);
      }
    }

    var created = new List<PermissionAssignment>(assignments.Count);

    // Serialize concurrent replaces of the same principal with the keyed lock, then perform
    // the read → delete → insert → change-log atomically inside a transaction. The lock
    // prevents two replaces from interleaving (single instance); the transaction keeps the
    // batch all-or-nothing.
    var lockKey = $"{principalKind}:{principalId}";
    await using (await _asyncLock.AcquireAsync(lockKey, cancellationToken))
    {
      try
      {
        await _appDb.ExecuteInTransaction(async () =>
        {
          var existing = await _appDb.PermissionAssignments
            .IgnoreQueryFilters()
            .Where(x => x.PrincipalKind == principalKind && x.PrincipalId == principalId)
            .ToListAsync(cancellationToken);

          var toDelete = existing.Where(x =>
              IsVisibleToTenant(x, tenantId, effectivePermissions) &&
              replacedScopeKinds.Contains(x.ScopeKind))
            .ToList();

          foreach (var existingAssignment in toDelete)
          {
            _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
              AuthorizationChangeLogActions.PermissionAssignmentDeleted,
              actor,
              AuthorizationChangeLogTargetTypes.PermissionAssignment,
              existingAssignment.Id,
              existingAssignment.OwningTenantId,
              before: new PermissionAssignmentSnapshot(
                existingAssignment.PermissionName, existingAssignment.Effect,
                existingAssignment.ScopeKind, existingAssignment.ScopeId)));

            _appDb.PermissionAssignments.Remove(existingAssignment);
          }

          foreach (var request in assignments)
          {
            var assignment = PermissionAssignment.CreateGrant(
              principalKind,
              principalId,
              request.PermissionName,
              request.ScopeKind,
              NormalizeScopeId(request.ScopeKind, request.ScopeId, tenantId),
              tenantId,
              actor,
              request.Effect,
              null,
              request.IsEnabled);

            _appDb.PermissionAssignments.Add(assignment);
            created.Add(assignment);
          }

          await _appDb.SaveChangesAsync(cancellationToken);

          for (var i = 0; i < created.Count; i++)
          {
            var assignment = created[i];
            var request = assignments[i];
            _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
              AuthorizationChangeLogActions.PermissionAssignmentCreated,
              actor,
              AuthorizationChangeLogTargetTypes.PermissionAssignment,
              assignment.Id,
              assignment.OwningTenantId,
              after: new PermissionAssignmentSnapshot(
                request.PermissionName, request.Effect, request.ScopeKind, request.ScopeId)));
          }

          await _appDb.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
      }
      catch (DbUpdateException)
      {
        foreach (var request in assignments)
        {
          if (await AssignmentExists(
            principalKind,
            principalId,
            request.PermissionName,
            request.Effect,
            request.ScopeKind,
            NormalizeScopeId(request.ScopeKind, request.ScopeId, tenantId),
            cancellationToken))
          {
            return HttpResult.Fail(HttpResultErrorCode.Conflict, "An identical permission assignment already exists.");
          }
        }

        throw;
      }
    }

    return HttpResult.Ok();
  }

  public async Task<HttpResult<InternalDtos.PermissionAssignmentDto>> Update(
    Guid assignmentId,
    InternalDtos.UpdatePermissionAssignmentRequestDto request,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken = default)
  {
    var assignment = await _appDb.PermissionAssignments
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == assignmentId, cancellationToken);

    if (assignment is null)
    {
      return HttpResult.Fail<InternalDtos.PermissionAssignmentDto>(
        HttpResultErrorCode.NotFound, "Permission assignment not found.");
    }

    var effectivePermissions = await GetEffectivePermissions(actor, tenantId, cancellationToken);

    if (!IsVisibleToTenant(assignment, tenantId, effectivePermissions))
    {
      return HttpResult.Fail<InternalDtos.PermissionAssignmentDto>(
        HttpResultErrorCode.NotFound, "Permission assignment not found.");
    }

    var targetIsServerServiceAccount = await IsServerServiceAccount(assignment.PrincipalKind, assignment.PrincipalId, cancellationToken);

    if (ValidateServerServiceAccountTarget(targetIsServerServiceAccount, effectivePermissions) is { IsSuccess: false } targetError)
    {
      return HttpResult.Fail<InternalDtos.PermissionAssignmentDto>(targetError.ErrorCode, targetError.Reason);
    }

    if (ValidateWriteAuthority(assignment.Effect, assignment.ScopeKind, effectivePermissions) is { } existingAuthorityError)
    {
      return HttpResult.Fail<InternalDtos.PermissionAssignmentDto>(existingAuthorityError.Code, existingAuthorityError.Reason);
    }

    if (ValidateWriteAuthority(request.Effect, request.ScopeKind, effectivePermissions) is { } authorityError)
    {
      return HttpResult.Fail<InternalDtos.PermissionAssignmentDto>(authorityError.Code, authorityError.Reason);
    }

    if (await ValidatePermissionScope(request.PermissionName, request.ScopeKind, request.ScopeId, request.Effect, tenantId,
      targetIsServerServiceAccount, cancellationToken) is { } scopeError)
    {
      return HttpResult.Fail<InternalDtos.PermissionAssignmentDto>(scopeError.Code, scopeError.Reason);
    }

    if (await ValidateCredentialPrincipalScope(
      assignment.PrincipalKind, assignment.PrincipalId, request.PermissionName,
      request.ScopeKind, request.ScopeId, request.Effect, cancellationToken) is { } credentialScopeError)
    {
      return HttpResult.Fail<InternalDtos.PermissionAssignmentDto>(HttpResultErrorCode.BadRequest, credentialScopeError);
    }

    if (IsSelf(assignment, actor.PrincipalId))
    {
      // The replacement is transient: it only feeds the self-protection check and is never
      // persisted. The live row's creator columns are left untouched below.
      var replacement = PermissionAssignment.CreateGrant(
        assignment.PrincipalKind,
        assignment.PrincipalId,
        request.PermissionName,
        request.ScopeKind,
        NormalizeScopeId(request.ScopeKind, request.ScopeId, tenantId),
        tenantId,
        createdBy: null,
        request.Effect,
        request.Notes,
        request.IsEnabled);

      replacement.Id = assignment.Id;
      if (await FindViolatedSelfProtected(
        actor,
        tenantId,
        direct => [
          .. direct.Where(row => row.Id != assignmentId),
          replacement
        ],
        cancellationToken) is { } violation)
      {
        return HttpResult.Fail<InternalDtos.PermissionAssignmentDto>(HttpResultErrorCode.BadRequest, violation);
      }
    }

    var before = new PermissionAssignmentSnapshot(
      assignment.PermissionName, assignment.Effect, assignment.ScopeKind, assignment.ScopeId);

    assignment.PermissionName = request.PermissionName;
    assignment.Effect = request.Effect;
    assignment.ScopeKind = request.ScopeKind;
    assignment.ScopeId = NormalizeScopeId(request.ScopeKind, request.ScopeId, tenantId);
    assignment.Notes = request.Notes;
    assignment.IsEnabled = request.IsEnabled;
    assignment.OwningTenantId = request.ScopeKind == PermissionScopeKind.Server ? null : tenantId;

    _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.PermissionAssignmentUpdated,
      actor,
      AuthorizationChangeLogTargetTypes.PermissionAssignment,
      assignment.Id,
      assignment.OwningTenantId,
      before: before,
      after: new PermissionAssignmentSnapshot(
        assignment.PermissionName, assignment.Effect, assignment.ScopeKind, assignment.ScopeId)));

    await _appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok(MapToDto(assignment));
  }

  private static IReadOnlyList<PermissionRule> CreateSelfProtectionRules(
    IReadOnlyCollection<PermissionAssignment> directAssignments,
    IReadOnlyCollection<PermissionAssignment> groupAssignments,
    Guid tenantId) =>
    [
      .. PermissionRuleFactory.CreateDirectRules(directAssignments, tenantId),
      .. PermissionRuleFactory.CreateGroupRules(groupAssignments, tenantId)
    ];

  private static bool IsSelf(PermissionAssignment assignment, Guid actorPrincipalId) =>
    assignment.PrincipalKind == PermissionPrincipalKind.User && assignment.PrincipalId == actorPrincipalId;

  /// <summary>
  /// Tenant isolation: tenant-owned rows are visible only to the owning tenant; server-scoped
  /// rows require the corresponding server read or write permission.
  /// </summary>
  private static bool IsVisibleToTenant(
    PermissionAssignment assignment,
    Guid tenantId,
    IReadOnlySet<string> effectivePermissions) =>
    assignment.OwningTenantId == tenantId ||
    (assignment.OwningTenantId is null &&
      (effectivePermissions.Contains(PermissionNames.ServerPermissionsRead) ||
       effectivePermissions.Contains(PermissionNames.ServerPermissionsWrite)));

  private static InternalDtos.PermissionAssignmentDto MapToDto(PermissionAssignment assignment)
  {
    return new InternalDtos.PermissionAssignmentDto(
      assignment.Id,
      assignment.PrincipalKind,
      assignment.PrincipalId,
      assignment.PermissionName,
      assignment.Effect,
      assignment.ScopeKind,
      assignment.ScopeId,
      assignment.Notes,
      assignment.IsEnabled,
      assignment.CreatedAt);
  }

  /// <summary>
  /// Server scope needs no target; tenant scope targets the acting user's tenant.
  /// </summary>
  private static Guid? NormalizeScopeId(PermissionScopeKind scopeKind, Guid? scopeId, Guid tenantId) => scopeKind switch
  {
    PermissionScopeKind.Server => null,
    PermissionScopeKind.Tenant => tenantId,
    _ => scopeId
  };

  /// <summary>
  /// Server-scoped service accounts operate cross-tenant by design, so assigning permissions
  /// to one is a server-scope operation: only a caller with ServerPermissionsWrite may target
  /// a server service account as a principal, regardless of the assignment's own scope kind.
  /// This prevents a tenant admin from attaching a tenant-scoped grant to a server account
  /// (which would strip its opt-in bypass or shadow its cross-tenant reach).
  /// </summary>
  private static HttpResult ValidateServerServiceAccountTarget(
    bool targetIsServerServiceAccount,
    IReadOnlySet<string> effectivePermissions)
  {
    if (targetIsServerServiceAccount && !effectivePermissions.Contains(PermissionNames.ServerPermissionsWrite))
    {
      return HttpResult.Fail(HttpResultErrorCode.Forbidden,
        $"The '{PermissionNames.ServerPermissionsWrite}' permission is required to manage assignments for a server service account.");
    }

    return HttpResult.Ok();
  }

  /// <summary>
  /// Checks the actor's write/deny management permissions for the target scope. This is
  /// delegated administration by design: a holder of <c>tenant.permissions.write</c> may grant
  /// permissions they do not themselves hold (making it de facto full tenant admin). The one
  /// exception is user provisioning: <c>UsersController.Create</c> additionally gates the
  /// TenantAdministrator preset so a <c>tenant.users.write</c> holder cannot mint a new
  /// permission-manager.
  /// </summary>
  private static (HttpResultErrorCode Code, string Reason)? ValidateWriteAuthority(
    PermissionEffect effect,
    PermissionScopeKind scopeKind,
    IReadOnlySet<string> effectivePermissions)
  {
    var requiredPermission = scopeKind == PermissionScopeKind.Server
      ? PermissionNames.ServerPermissionsWrite
      : PermissionNames.TenantPermissionsWrite;
    if (!effectivePermissions.Contains(requiredPermission))
    {
      return (HttpResultErrorCode.Forbidden, $"The '{requiredPermission}' permission is required to manage {scopeKind.ToString().ToLowerInvariant()}-scoped assignments.");
    }

    if (effect == PermissionEffect.Allow)
    {
      return null;
    }

    if (!effectivePermissions.Contains(PermissionNames.TenantPermissionsDeny))
    {
      return (HttpResultErrorCode.Forbidden, $"The '{PermissionNames.TenantPermissionsDeny}' permission is required to manage {effect.ToString().ToLowerInvariant()} assignments.");
    }

    return null;
  }

  private Task<bool> AssignmentExists(
    PermissionPrincipalKind principalKind,
    Guid principalId,
    string permissionName,
    PermissionEffect effect,
    PermissionScopeKind scopeKind,
    Guid? scopeId,
    CancellationToken cancellationToken) =>
    AssignmentExists(
      principalKind,
      principalId,
      new AssignmentKey(permissionName, effect, scopeKind, scopeId),
      cancellationToken);

  private Task<bool> AssignmentExists(
    PermissionPrincipalKind principalKind,
    Guid principalId,
    AssignmentKey key,
    CancellationToken cancellationToken) =>
    _appDb.PermissionAssignments
      .IgnoreQueryFilters()
      .AnyAsync(x => x.PrincipalKind == principalKind &&
                     x.PrincipalId == principalId &&
                     x.PermissionName == key.PermissionName &&
                     x.Effect == key.Effect &&
                     x.ScopeKind == key.ScopeKind &&
                     x.ScopeId == key.ScopeId,
        cancellationToken);

  private async Task<string?> FindViolatedSelfProtected(
    PrincipalDescriptor actor,
    Guid tenantId,
    Func<IReadOnlyList<PermissionAssignment>, IReadOnlyList<PermissionAssignment>> mutate,
    CancellationToken cancellationToken)
  {
    var directAssignments = await _appDb.PermissionAssignments
      .IgnoreQueryFilters()
      .AsNoTracking()
      .Where(row => row.PrincipalKind == PermissionPrincipalKind.User &&
                    row.PrincipalId == actor.PrincipalId)
      .ToListAsync(cancellationToken);
    var groupIds = await _appDb.UserGroupMembers
      .IgnoreQueryFilters()
      .AsNoTracking()
      .Where(member => member.UserId == actor.PrincipalId)
      .Select(member => member.UserGroupId)
      .ToListAsync(cancellationToken);
    var groupAssignments = groupIds.Count == 0
      ? []
      : await _appDb.PermissionAssignments
        .IgnoreQueryFilters()
        .AsNoTracking()
        .Where(row => row.PrincipalKind == PermissionPrincipalKind.UserGroup &&
                      groupIds.Contains(row.PrincipalId))
        .ToListAsync(cancellationToken);

    var postDirectAssignments = mutate(directAssignments);
    var beforeRules = CreateSelfProtectionRules(directAssignments, groupAssignments, tenantId);
    var afterRules = CreateSelfProtectionRules(postDirectAssignments, groupAssignments, tenantId);
    var serverResource = new ResourceDescriptor(PermissionScopeKind.Server);
    var tenantResource = new ResourceDescriptor(PermissionScopeKind.Tenant, tenantId, tenantId);

    foreach (var permissionName in PermissionCatalog.All
      .Where(entry => !entry.Value.SelfRemovable)
      .Select(entry => entry.Key))
    {
      var resource = PermissionCatalog.Get(permissionName)?.AllowedScopeKinds.Contains(PermissionScopeKind.Server) == true
        ? serverResource
        : tenantResource;
      var allowedBefore = _decisionEvaluator.EvaluateRules(beforeRules, permissionName, resource).Allowed;
      var allowedAfter = _decisionEvaluator.EvaluateRules(afterRules, permissionName, resource).Allowed;
      if (allowedBefore && !allowedAfter)
      {
        var metadata = PermissionCatalog.Get(permissionName)
          ?? throw new InvalidOperationException($"Permission '{permissionName}' is missing from the catalog.");
        return $"You cannot remove '{metadata.DisplayName}' from yourself; retaining it is required to continue managing permissions.";
      }
    }

    return null;
  }

  private async Task<IReadOnlySet<string>> GetEffectivePermissions(
    PrincipalDescriptor actor,
    Guid tenantId,
    CancellationToken cancellationToken)
  {
    var permissionNames = new[]
    {
      PermissionNames.ServerPermissionsRead,
      PermissionNames.ServerPermissionsWrite,
      PermissionNames.TenantPermissionsRead,
      PermissionNames.TenantPermissionsWrite,
      PermissionNames.TenantPermissionsDeny
    };
    var permissionRequests = new[]
    {
      new PermissionEvaluationRequest(permissionNames[0], new ResourceDescriptor(PermissionScopeKind.Server)),
      new PermissionEvaluationRequest(permissionNames[1], new ResourceDescriptor(PermissionScopeKind.Server)),
      new PermissionEvaluationRequest(permissionNames[2], new ResourceDescriptor(PermissionScopeKind.Tenant, tenantId, tenantId)),
      new PermissionEvaluationRequest(permissionNames[3], new ResourceDescriptor(PermissionScopeKind.Tenant, tenantId, tenantId)),
      new PermissionEvaluationRequest(permissionNames[4], new ResourceDescriptor(PermissionScopeKind.Tenant, tenantId, tenantId))
    };
    var decisions = await _permissionEvaluator.EvaluateBatch(
      actor,
      permissionRequests,
      cancellationToken);

    return permissionNames
      .Where((_, index) => decisions[permissionRequests[index]].Allowed)
      .ToHashSet(StringComparer.Ordinal);
  }

  /// <summary>
  /// Returns which of <paramref name="principals"/> are server-kind service accounts, the only
  /// principals that may hold Server-scope grants of tenant-addressable permissions.
  /// </summary>
  /// <remarks>
  /// Principal ids are only unique within a <see cref="PermissionPrincipalKind"/>, so the kind must
  /// travel with the id. A user whose id happens to match a service account id is not a service
  /// account and must not be classified as one.
  /// </remarks>
  private async Task<HashSet<(PermissionPrincipalKind Kind, Guid Id)>> GetServerServiceAccountPrincipals(
    IEnumerable<(PermissionPrincipalKind Kind, Guid Id)> principals,
    CancellationToken cancellationToken)
  {
    var candidates = principals
      .Where(x => x.Kind == PermissionPrincipalKind.ServiceAccount)
      .Distinct()
      .ToList();

    if (candidates.Count == 0)
    {
      return [];
    }

    var ids = candidates.Select(x => x.Id).ToList();
    var serverAccountIds = await _appDb.ServiceAccounts
      .Where(x => ids.Contains(x.Id) && x.Kind == ServiceAccountKind.Server)
      .Select(x => x.Id)
      .ToListAsync(cancellationToken);

    var serverIdSet = serverAccountIds.ToHashSet();
    return [.. candidates.Where(x => serverIdSet.Contains(x.Id))];
  }

  private async Task<bool> IsServerServiceAccount(
    PermissionPrincipalKind principalKind,
    Guid principalId,
    CancellationToken cancellationToken) =>
    (await GetServerServiceAccountPrincipals([(principalKind, principalId)], cancellationToken)).Contains((principalKind, principalId));

  /// <summary>
  /// Credential principals can't exceed their owning user's rights; validates the row against
  /// the owner at write time. Returns an error message, or <see langword="null"/> if valid.
  /// </summary>
  private async Task<string?> ValidateCredentialPrincipalScope(
    PermissionPrincipalKind principalKind,
    Guid principalId,
    string permissionName,
    PermissionScopeKind scopeKind,
    Guid? scopeId,
    PermissionEffect effect,
    CancellationToken cancellationToken)
  {
    Guid? ownerUserId = null;
    if (principalKind == PermissionPrincipalKind.LogonToken)
    {
      var token = await _appDb.LogonTokens
        .IgnoreQueryFilters()
        .Where(x => x.Id == principalId)
        .Select(x => new { x.DeviceId, x.UserId })
        .FirstOrDefaultAsync(cancellationToken);

      if (token is null)
      {
        return "Logon token not found.";
      }

      // Logon-token rules are only ever loaded when they are Device-scoped to the token's own
      // device, so every other row shape is inert. Reject them rather than report success for
      // a row that can never evaluate.
      if (scopeKind != PermissionScopeKind.Device || scopeId != token.DeviceId)
      {
        return "Logon token assignments must be Device-scoped to the token's device.";
      }

      ownerUserId = token.UserId;
    }
    else if (principalKind == PermissionPrincipalKind.PersonalAccessToken)
    {
      ownerUserId = await _appDb.PersonalAccessTokens
        .IgnoreQueryFilters()
        .Where(x => x.Id == principalId)
        .Select(x => (Guid?)x.UserId)
        .FirstOrDefaultAsync(cancellationToken);
    }

    if (ownerUserId is null)
    {
      return null;
    }

    var owner = await _appDb.Users
      .IgnoreQueryFilters()
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == ownerUserId, cancellationToken);
    if (owner is null || owner.TenantId == Guid.Empty)
    {
      return "Token owner not found.";
    }

    // A deny confers nothing, so there is no grant authority to check against the owner.
    if (effect == PermissionEffect.Deny)
    {
      return null;
    }

    var ownerPrincipal = new PrincipalDescriptor(
      PrincipalType: PrincipalType.User,
      PrincipalId: owner.Id,
      TenantId: owner.TenantId,
      AuthMethod: "credential-scope-validation");

    var validation = await _credentialScopeService.ValidateGrantableScopes(
      ownerPrincipal,
      owner.TenantId,
      [new InternalDtos.CredentialScopeDto(permissionName, scopeKind, scopeId)],
      cancellationToken);

    return validation.IsSuccess ? null : validation.Reason;
  }

  /// <summary>
  /// Validates that the permission exists in the catalog and that the requested scope kind is
  /// within its allowed whitelist. Returns a user-facing error message, or <see langword="null"/>
  /// when valid.
  /// </summary>
  private async Task<(HttpResultErrorCode Code, string Reason)?> ValidatePermissionScope(
    string permissionName,
    PermissionScopeKind scopeKind,
    Guid? scopeId,
    PermissionEffect effect,
    Guid tenantId,
    bool targetIsServerServiceAccount,
    CancellationToken cancellationToken)
  {
    var metadata = PermissionCatalog.Get(permissionName);
    if (metadata is null)
    {
      return (HttpResultErrorCode.BadRequest, $"Unknown permission name: {permissionName}");
    }

    if (!metadata.AllowedScopeKinds.Contains(scopeKind))
    {
      return (HttpResultErrorCode.BadRequest, $"Permission '{metadata.DisplayName}' cannot be assigned at {scopeKind} scope.");
    }

    if (scopeKind is PermissionScopeKind.Device or PermissionScopeKind.DeviceGroup or PermissionScopeKind.CustomerTenant or PermissionScopeKind.UserGroup && !scopeId.HasValue)
    {
      return (HttpResultErrorCode.BadRequest, $"ScopeId is required for scope kind: {scopeKind}");
    }

    if (scopeKind == PermissionScopeKind.Server && scopeId.HasValue)
    {
      return (HttpResultErrorCode.BadRequest, $"ScopeId must be null for {scopeKind} scope.");
    }

    // Server-scope allows of tenant-addressable permissions reach every tenant, so only the one
    // principal kind with no tenant binding may hold them. Denies and server administration
    // permissions (whose only legal scope is already Server) are exempt.
    if (effect == PermissionEffect.Allow
      && scopeKind == PermissionScopeKind.Server
      && metadata.AllowsTenantScope
      && !targetIsServerServiceAccount)
    {
      return (HttpResultErrorCode.BadRequest,
        "Server-scoped resource permissions can only be assigned to server service accounts.");
    }

    if (scopeKind == PermissionScopeKind.Device && !await _appDb.Devices.AnyAsync(x => x.Id == scopeId && x.TenantId == tenantId, cancellationToken))
    {
      return (HttpResultErrorCode.BadRequest, "The selected device does not belong to the current tenant.");
    }

    if (scopeKind == PermissionScopeKind.DeviceGroup && !await _appDb.DeviceGroups.AnyAsync(x => x.Id == scopeId && x.TenantId == tenantId, cancellationToken))
    {
      return (HttpResultErrorCode.BadRequest, "The selected device group does not belong to the current tenant.");
    }

    if (scopeKind == PermissionScopeKind.CustomerTenant && !await _appDb.Customers.AnyAsync(x => x.Id == scopeId && x.TenantId == tenantId, cancellationToken))
    {
      return (HttpResultErrorCode.BadRequest, "The selected customer does not belong to the current tenant.");
    }

    if (scopeKind == PermissionScopeKind.UserGroup && !await _appDb.UserGroups.AnyAsync(x => x.Id == scopeId && x.TenantId == tenantId, cancellationToken))
    {
      return (HttpResultErrorCode.BadRequest, "The selected user group does not belong to the current tenant.");
    }

    return null;
  }

  private async Task<bool> ValidatePrincipalExists(
    PermissionPrincipalKind principalKind,
    Guid principalId,
    Guid tenantId,
    CancellationToken cancellationToken)
  {
    return principalKind switch
    {
      PermissionPrincipalKind.User => await _appDb.Users
        .AnyAsync(x => x.Id == principalId && x.TenantId == tenantId, cancellationToken),
      PermissionPrincipalKind.UserGroup => await _appDb.UserGroups
        .AnyAsync(x => x.Id == principalId && x.TenantId == tenantId, cancellationToken),
      PermissionPrincipalKind.ServiceAccount => await _appDb.ServiceAccounts
        .AnyAsync(x => x.Id == principalId &&
                       (x.Kind == ServiceAccountKind.Server ||
                        x.TenantId == tenantId && x.Kind == ServiceAccountKind.Tenant), cancellationToken),
      PermissionPrincipalKind.PersonalAccessToken => await _appDb.PersonalAccessTokens
        .AnyAsync(x => x.Id == principalId && x.User!.TenantId == tenantId, cancellationToken),
      PermissionPrincipalKind.LogonToken => await _appDb.LogonTokens
        .AnyAsync(x => x.Id == principalId && x.TenantId == tenantId, cancellationToken),
      _ => false
    };
  }

  private sealed record AssignmentKey(
    string PermissionName,
    PermissionEffect Effect,
    PermissionScopeKind ScopeKind,
    Guid? ScopeId);
}
