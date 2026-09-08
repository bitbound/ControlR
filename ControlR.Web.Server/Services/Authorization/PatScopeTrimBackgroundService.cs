using ControlR.Libraries.Hosting;
using ControlR.Web.Server.Authz.Permissions;

namespace ControlR.Web.Server.Services.Authorization;

/// <summary>
/// Periodically trims PAT scope rows that exceed the owner's current effective permissions.
/// Excess rows are already inert at evaluation time, so this is storage hygiene, not a
/// security boundary; each trim is recorded in the <see cref="AuthorizationChangeLog"/>.
/// </summary>
public class PatScopeTrimBackgroundService(
  IDbContextFactory<AppDb> dbContextFactory,
  IAuthorizationChangeLogFactory changeLogFactory,
  IServiceScopeFactory scopeFactory,
  TimeProvider timeProvider,
  ILogger<PeriodicBackgroundService> logger)
  : PeriodicBackgroundService(TimeSpan.FromMinutes(15), true, timeProvider, logger)
{
  private readonly IAuthorizationChangeLogFactory _changeLogFactory = changeLogFactory;
  private readonly IDbContextFactory<AppDb> _dbContextFactory = dbContextFactory;
  private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

  public async Task Sweep(CancellationToken cancellationToken)
  {
    await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var tokenIds = await db.PermissionAssignments
      .IgnoreQueryFilters()
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.PersonalAccessToken &&
                  x.IsEnabled)
      .Select(x => x.PrincipalId)
      .Distinct()
      .ToListAsync(cancellationToken);

    if (tokenIds.Count == 0)
    {
      return;
    }

    using var scope = _scopeFactory.CreateScope();
    var permissionEvaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();
    var resourceFactory = scope.ServiceProvider.GetRequiredService<IResourceDescriptorFactory>();

    foreach (var tokenId in tokenIds)
    {
      try
      {
        await ResolveAndTrimAsync(
          db,
          permissionEvaluator,
          resourceFactory,
          tokenId,
          cancellationToken);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception ex)
      {
        Logger.LogError(ex,
          "Error trimming scopes for personal access token {TokenId}.", tokenId);
      }
    }
  }

  protected override async Task HandleElapsed(CancellationToken stoppingToken)
  {
    await Sweep(stoppingToken);
  }

  protected override async Task OnStartingAsync(CancellationToken stoppingToken)
  {
    await Sweep(stoppingToken);
  }

  private async Task ResolveAndTrimAsync(
    AppDb db,
    IPermissionEvaluator permissionEvaluator,
    IResourceDescriptorFactory resourceFactory,
    Guid tokenId,
    CancellationToken cancellationToken)
  {
    var scopeRows = await db.PermissionAssignments
      .IgnoreQueryFilters()
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.PersonalAccessToken &&
                  x.PrincipalId == tokenId &&
                  x.IsEnabled)
      .ToListAsync(cancellationToken);

    if (scopeRows.Count == 0)
    {
      return;
    }

    var owner = await db.PersonalAccessTokens
      .IgnoreQueryFilters()
      .Where(x => x.Id == tokenId)
      .Select(x => new { x.UserId, UserTenantId = x.User!.TenantId })
      .FirstOrDefaultAsync(cancellationToken);

    if (owner is null)
    {
      Logger.LogWarning(
        "Cannot resolve owning user for personal access token {TokenId}. Skipping trim.",
        tokenId);
      return;
    }

    var principal = new PrincipalDescriptor(
      PrincipalType: PrincipalType.User,
      PrincipalId: owner.UserId,
      TenantId: owner.UserTenantId,
      AuthMethod: "pat-scope-trim");

    var rowsWithResources = new List<(PermissionAssignment Row, ResourceDescriptor Resource)>();
    foreach (var row in scopeRows)
    {
      var resource = await resourceFactory.CreateScope(
        row.ScopeKind,
        row.ScopeId,
        row.OwningTenantId ?? owner.UserTenantId,
        cancellationToken);
      if (resource is not null)
      {
        rowsWithResources.Add((row, resource));
      }
    }

    var requests = rowsWithResources
      .Select(item => new PermissionEvaluationRequest(item.Row.PermissionName, item.Resource))
      .ToList();
    var decisions = await permissionEvaluator.EvaluateBatch(
      principal,
      requests,
      cancellationToken);
    var coveredRowIds = rowsWithResources
      .Where((_, index) => decisions[requests[index]].Allowed)
      .Select(item => item.Row.Id)
      .ToHashSet();
    // Trimming removes excess reach, and only allows carry reach. Denies are deliberately
    // permitted to exceed the owner's permissions (the write gate skips owner-authority for
    // them), so owner-coverage is the wrong test and trimming them would undo accepted writes.
    var excessRows = scopeRows
      .Where(row => row.Effect != PermissionEffect.Deny && !coveredRowIds.Contains(row.Id))
      .ToList();

    if (excessRows.Count == 0)
    {
      return;
    }

    foreach (var row in excessRows)
    {
      db.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
        AuthorizationChangeLogActions.CredentialScopeTrim,
        actor: null,
        AuthorizationChangeLogTargetTypes.PermissionAssignment,
        row.Id,
        row.OwningTenantId,
        before: new CredentialScopeSnapshot(
          row.PermissionName, row.ScopeKind, row.ScopeId)));

      db.PermissionAssignments.Remove(row);
    }

    await db.SaveChangesAsync(cancellationToken);

    Logger.LogInformation(
      "Trimmed {Count} excess scope row(s) from personal access token {TokenId}.",
      excessRows.Count, tokenId);
  }
}
