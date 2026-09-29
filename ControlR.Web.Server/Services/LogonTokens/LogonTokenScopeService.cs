using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.Authorization;

namespace ControlR.Web.Server.Services.LogonTokens;

public interface ILogonTokenScopeService
{
  Task<HttpResult<LogonTokenResult>> CreateTokenWithScopes(
    LogonTokenCreationRequest request,
    PrincipalDescriptor creator,
    CancellationToken cancellationToken);
}

public class LogonTokenScopeService(
  AppDb appDb,
  IAuthorizationChangeLogFactory changeLogFactory,
  ICredentialScopeService credentialScopeService,
  ILogonTokenProvider logonTokenProvider,
  ILogger<LogonTokenScopeService> logger) : ILogonTokenScopeService
{
  private readonly AppDb _appDb = appDb;
  private readonly IAuthorizationChangeLogFactory _changeLogFactory = changeLogFactory;
  private readonly ICredentialScopeService _credentialScopeService = credentialScopeService;
  private readonly ILogger<LogonTokenScopeService> _logger = logger;
  private readonly ILogonTokenProvider _logonTokenProvider = logonTokenProvider;

  public async Task<HttpResult<LogonTokenResult>> CreateTokenWithScopes(
    LogonTokenCreationRequest request,
    PrincipalDescriptor creator,
    CancellationToken cancellationToken)
  {
    var preparation = await PrepareScopes(
      request.Scopes,
      request.AllowedDesktopSessionIds,
      request.DeviceId,
      request.TenantId,
      creator,
      cancellationToken);
    if (!preparation.IsSuccess)
    {
      return HttpResult.Fail<LogonTokenResult>(preparation.ErrorCode, preparation.Reason);
    }

    return await CreateTokenAndWriteScopes(
      preparation.Value,
      request,
      cancellationToken);
  }

  /// <summary>
  /// Best-effort removal of a token whose explicit-scope write failed; otherwise the
  /// expired-token cleanup will reclaim it.
  /// </summary>
  private async Task CleanupOrphanedToken(Guid tokenId, int attemptedScopeCount, CancellationToken cancellationToken)
  {
    try
    {
      var orphaned = await _appDb.LogonTokens.FirstOrDefaultAsync(x => x.Id == tokenId, cancellationToken);
      if (orphaned is null)
      {
        return;
      }

      _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
        AuthorizationChangeLogActions.CredentialScopeSetFailed,
        actor: null,
        AuthorizationChangeLogTargetTypes.LogonToken,
        tokenId,
        orphaned.TenantId,
        after: new CredentialScopeSetFailureSummary(attemptedScopeCount, "scope-write-failed")));

      _appDb.LogonTokens.Remove(orphaned);
      await _appDb.SaveChangesAsync(cancellationToken);

      _logger.LogInformation("Removed orphaned logon token {TokenId} after failed credential scope write.", tokenId);
    }
    catch (Exception ex)
    {
      _logger.LogError(
        ex,
        "Failed to remove orphaned logon token {TokenId} after a failed credential scope write. It will be removed by the expired-token cleanup.",
        tokenId);
    }
  }

  private Task<HttpResult<LogonTokenResult>> CreateToken(
    LogonTokenCreationRequest request,
    bool writeBaselineGrants,
    CancellationToken cancellationToken)
  {
    if (request.UserCorrelationId is not null)
    {
      return _logonTokenProvider.CreateTokenForExternal(
        request.DeviceId,
        request.TenantId,
        request.UserCorrelationId,
        request.ExpirationMinutes,
        userDisplayName: request.UserDisplayName,
        sessionCorrelationId: request.SessionCorrelationId,
        writeBaselineGrants: writeBaselineGrants,
        cancellationToken: cancellationToken,
        allowedDesktopSessionIds: request.AllowedDesktopSessionIds,
        sessionExpirationMinutes: request.SessionExpirationMinutes);
    }

    var userId = request.UserId
      ?? throw new InvalidOperationException("LogonTokenCreationRequest must have either UserId or UserCorrelationId.");

    return _logonTokenProvider.CreateToken(
      request.DeviceId,
      request.TenantId,
      userId,
      request.ExpirationMinutes,
      sessionCorrelationId: request.SessionCorrelationId,
      writeBaselineGrants: writeBaselineGrants,
      cancellationToken: cancellationToken,
      allowedDesktopSessionIds: request.AllowedDesktopSessionIds,
      sessionExpirationMinutes: request.SessionExpirationMinutes);
  }

  /// <summary>
  /// Creates the token, writes explicit scopes, and removes the token if the scope write
  /// fails, so the secret is never returned unless grants are in place.
  /// </summary>
  private async Task<HttpResult<LogonTokenResult>> CreateTokenAndWriteScopes(
    ScopePreparation preparation,
    LogonTokenCreationRequest request,
    CancellationToken cancellationToken)
  {
    var result = await CreateToken(request, preparation.ExplicitScopes is null, cancellationToken);
    if (!result.IsSuccess || preparation.ExplicitScopes is null)
    {
      return result;
    }

    try
    {
      await _credentialScopeService.WriteLogonTokenScopes(
        result.Value.TokenId,
        request.DeviceId,
        request.TenantId,
        preparation.Creator,
        preparation.ExplicitScopes,
        cancellationToken);

      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to write credential scopes for logon token {TokenId}.", result.Value.TokenId);
      await CleanupOrphanedToken(result.Value.TokenId, preparation.ExplicitScopes.Count, cancellationToken);
      return HttpResult.Fail<LogonTokenResult>(
        HttpResultErrorCode.InternalServerError,
        "Failed to write credential scopes.");
    }
  }

  /// <summary>
  /// Normalizes and validates the explicit scope list (unions in device.read) and verifies
  /// the creator holds each requested permission, preventing escalation beyond the baseline.
  /// </summary>
  private async Task<HttpResult<ScopePreparation>> PrepareScopes(
    IReadOnlyList<InternalDtos.CredentialScopeDto>? requestedScopes,
    IReadOnlyList<int>? allowedDesktopSessionIds,
    Guid deviceId,
    Guid tenantId,
    PrincipalDescriptor creator,
    CancellationToken cancellationToken)
  {
    if (requestedScopes is { Count: > DtoLimits.PermissionsMaxLength })
    {
      return HttpResult.Fail<ScopePreparation>(
        HttpResultErrorCode.BadRequest,
        $"Requested scopes cannot exceed {DtoLimits.PermissionsMaxLength}.");
    }

    if (allowedDesktopSessionIds is { Count: 0 })
    {
      return HttpResult.Fail<ScopePreparation>(
        HttpResultErrorCode.BadRequest,
        "AllowedDesktopSessionIds must contain at least one session ID when supplied.");
    }

    if (allowedDesktopSessionIds is { Count: > LogonTokenCreationRequest.MaxAllowedDesktopSessionIds })
    {
      return HttpResult.Fail<ScopePreparation>(
        HttpResultErrorCode.BadRequest,
        "AllowedDesktopSessionIds cannot contain more than 32 session IDs.");
    }

    if (allowedDesktopSessionIds?.Any(x => x < 0) == true)
    {
      return HttpResult.Fail<ScopePreparation>(
        HttpResultErrorCode.BadRequest,
        "AllowedDesktopSessionIds cannot contain negative session IDs.");
    }

    if (requestedScopes is not { Count: > 0 })
    {
      return HttpResult.Ok(new ScopePreparation(null, creator));
    }

    var scopes = new List<InternalDtos.CredentialScopeDto>();
    var seen = new HashSet<(string PermissionName, PermissionScopeKind ScopeKind, Guid? ScopeId)>();
    foreach (var scope in requestedScopes)
    {
      if (!PermissionCatalog.Exists(scope.PermissionName))
      {
        return HttpResult.Fail<ScopePreparation>(
          HttpResultErrorCode.BadRequest,
          $"Unknown permission name: {scope.PermissionName}");
      }

      // Logon tokens are hard-bound to their device; a grant for any other device would be
      // inert at evaluation time, so reject it up front instead of writing a dead row.
      if (scope.ScopeKind == PermissionScopeKind.Device && scope.ScopeId != deviceId)
      {
        return HttpResult.Fail<ScopePreparation>(
          HttpResultErrorCode.BadRequest,
          "Logon token scopes must target the token's device.");
      }

      if (seen.Add((scope.PermissionName, scope.ScopeKind, scope.ScopeId)))
      {
        scopes.Add(scope);
      }
    }

    var hasDeviceRead = scopes.Any(x =>
      x.PermissionName == PermissionNames.DeviceRead &&
      x.ScopeKind == PermissionScopeKind.Device &&
      x.ScopeId == deviceId);
    if (!hasDeviceRead)
    {
      scopes.Add(new InternalDtos.CredentialScopeDto(PermissionNames.DeviceRead, PermissionScopeKind.Device, deviceId));
    }

    return await ValidateScopes(scopes, tenantId, creator, cancellationToken);
  }

  private async Task<HttpResult<ScopePreparation>> ValidateScopes(
    IReadOnlyList<InternalDtos.CredentialScopeDto> scopes,
    Guid tenantId,
    PrincipalDescriptor creator,
    CancellationToken cancellationToken)
  {
    var validation = await _credentialScopeService.ValidateLogonTokenScopes(
      creator, tenantId, scopes, cancellationToken);
    if (!validation.IsSuccess)
    {
      return HttpResult.Fail<ScopePreparation>(validation.ErrorCode, validation.Reason);
    }

    return HttpResult.Ok(new ScopePreparation(scopes, creator));
  }

  private sealed record ScopePreparation(
    IReadOnlyList<InternalDtos.CredentialScopeDto>? ExplicitScopes,
    PrincipalDescriptor Creator);
}
