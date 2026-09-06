using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization.PermissionRules;

namespace ControlR.Web.Server.Services.Authorization;

public interface IPermissionEvaluationContextLoader
{
  Task<PermissionEvaluationContext> Load(
    PrincipalDescriptor principal,
    CancellationToken cancellationToken);
}

public sealed class PermissionEvaluationContextLoader(
  IDbContextFactory<AppDb> dbContextFactory) : IPermissionEvaluationContextLoader
{
  private readonly IDbContextFactory<AppDb> _dbContextFactory = dbContextFactory;

  public async Task<PermissionEvaluationContext> Load(
    PrincipalDescriptor principal,
    CancellationToken cancellationToken)
  {
    await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    if (principal.PrincipalType is not PrincipalType.ServerServiceAccount &&
        !principal.TenantId.HasValue)
    {
      return new PermissionEvaluationContext(principal, false, [], [], false);
    }

    if (principal.PrincipalType == PrincipalType.ServerServiceAccount)
    {
      var accessMode = await db.ServiceAccounts
        .IgnoreQueryFilters()
        .AsNoTracking()
        .Where(account => account.Id == principal.PrincipalId)
        .Select(account => account.AccessMode)
        .FirstOrDefaultAsync(cancellationToken);

      if (accessMode == ServiceAccountAccessMode.Unrestricted)
      {
        return PermissionEvaluationContext.Bypass(principal);
      }
    }

    var ownerRules = await LoadOwnerRules(db, principal, cancellationToken);
    if (!principal.IsCredentialScoped)
    {
      return new PermissionEvaluationContext(principal, false, ownerRules, ownerRules, false);
    }

    if (!principal.CredentialId.HasValue)
    {
      return new PermissionEvaluationContext(principal, false, ownerRules, [], false);
    }

    if (principal.CredentialType == CredentialType.LogonToken)
    {
      var tokenRules = await LoadCredentialRules(
        db,
        PermissionPrincipalKind.LogonToken,
        principal.CredentialId.Value,
        RuleSource.LogonTokenGrant,
        SourcePriority.CredentialLogonToken,
        principal.TenantId,
        cancellationToken);

      if (!principal.DeviceScopeId.HasValue)
      {
        tokenRules = [];
      }
      else
      {
        tokenRules = [.. tokenRules.Where(rule =>
          rule.ScopeKind == PermissionScopeKind.Device &&
          rule.ScopeId == principal.DeviceScopeId.Value)];
      }

      return new PermissionEvaluationContext(principal, false, ownerRules, tokenRules, false);
    }

    // Project nullable so a missing (deleted) token row yields null, not the enum default,
    // and falls through to the restricted path below.
    var permissionMode = await db.PersonalAccessTokens
      .IgnoreQueryFilters()
      .AsNoTracking()
      .Where(token => token.Id == principal.CredentialId.Value)
      .Select(token => (PersonalAccessTokenPermissionMode?)token.PermissionMode)
      .FirstOrDefaultAsync(cancellationToken);

    if (permissionMode == PersonalAccessTokenPermissionMode.InheritOwner)
    {
      return new PermissionEvaluationContext(principal, false, ownerRules, ownerRules, false);
    }

    var patRules = await LoadCredentialRules(
      db,
      PermissionPrincipalKind.PersonalAccessToken,
      principal.CredentialId.Value,
      RuleSource.PatGrant,
      SourcePriority.CredentialPat,
      principal.TenantId,
      cancellationToken);

    return new PermissionEvaluationContext(principal, false, ownerRules, patRules, true);
  }

  private static async Task<IReadOnlyList<PermissionRule>> LoadCredentialRules(
    AppDb db,
    PermissionPrincipalKind principalKind,
    Guid principalId,
    RuleSource source,
    SourcePriority priority,
    Guid? tenantId,
    CancellationToken cancellationToken)
  {
    var assignments = await db.PermissionAssignments
      .IgnoreQueryFilters()
      .AsNoTracking()
      .Where(assignment => assignment.PrincipalKind == principalKind &&
                           assignment.PrincipalId == principalId &&
                           assignment.IsEnabled)
      .ToListAsync(cancellationToken);

    return PermissionRuleFactory.CreateCredentialRules(assignments, tenantId, source, priority);
  }

  private static async Task<IReadOnlyList<PermissionRule>> LoadOwnerRules(
    AppDb db,
    PrincipalDescriptor principal,
    CancellationToken cancellationToken)
  {
    var principalKind = ResolvePrincipalKind(principal.PrincipalType);
    var assignments = await db.PermissionAssignments
      .IgnoreQueryFilters()
      .AsNoTracking()
      .Where(assignment => assignment.PrincipalKind == principalKind &&
                           assignment.PrincipalId == principal.PrincipalId &&
                           assignment.IsEnabled)
      .ToListAsync(cancellationToken);

    var tenantFilter = principal.PrincipalType is
      PrincipalType.User or PrincipalType.UserGroup or PrincipalType.TenantServiceAccount
        ? principal.TenantId
        : null;

    var rules = PermissionRuleFactory.CreateDirectRules(assignments, tenantFilter).ToList();

    if (principal.PrincipalType != PrincipalType.User)
    {
      return rules;
    }

    var groupIds = await db.UserGroupMembers
      .IgnoreQueryFilters()
      .AsNoTracking()
      .Where(member => member.UserId == principal.PrincipalId)
      .Select(member => member.UserGroupId)
      .ToListAsync(cancellationToken);

    if (groupIds.Count == 0)
    {
      return rules;
    }

    var groupAssignments = await db.PermissionAssignments
      .IgnoreQueryFilters()
      .AsNoTracking()
      .Where(assignment => assignment.PrincipalKind == PermissionPrincipalKind.UserGroup &&
                           groupIds.Contains(assignment.PrincipalId) &&
                           assignment.IsEnabled)
      .ToListAsync(cancellationToken);

    rules.AddRange(PermissionRuleFactory.CreateGroupRules(groupAssignments, tenantFilter));

    return rules;
  }

  private static PermissionPrincipalKind ResolvePrincipalKind(PrincipalType principalType) =>
    principalType switch
    {
      PrincipalType.TenantServiceAccount or PrincipalType.ServerServiceAccount =>
        PermissionPrincipalKind.ServiceAccount,
      PrincipalType.UserGroup => PermissionPrincipalKind.UserGroup,
      _ => PermissionPrincipalKind.User
    };
}
