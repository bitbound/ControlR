using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Server.Data.Enums;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.Settings;

namespace ControlR.Web.Server.Services.LogonTokens;

public interface ILogonTokenProvider
{
  Task<HttpResult<LogonTokenResult>> CreateToken(
    Guid deviceId,
    Guid tenantId,
    Guid userId,
    int expirationMinutes = 5,
    string? userCorrelationId = null,
    string? sessionCorrelationId = null,
    bool writeBaselineGrants = true,
    IReadOnlyList<int>? allowedDesktopSessionIds = null,
    int sessionExpirationMinutes = DtoLimits.SessionExpirationMinutesDefault,
    CancellationToken cancellationToken = default);

  Task<HttpResult<LogonTokenResult>> CreateTokenForExternal(
    Guid deviceId,
    Guid tenantId,
    string userCorrelationId,
    int expirationMinutes = 5,
    string? userDisplayName = null,
    string? sessionCorrelationId = null,
    bool writeBaselineGrants = true,
    IReadOnlyList<int>? allowedDesktopSessionIds = null,
    int sessionExpirationMinutes = DtoLimits.SessionExpirationMinutesDefault,
    CancellationToken cancellationToken = default);

  Task<LogonTokenValidationResult> ValidateAndConsumeToken(string token, Guid deviceId, CancellationToken cancellationToken = default);
  Task<LogonTokenValidationResult> ValidateToken(string token, CancellationToken cancellationToken = default);
}

public class LogonTokenProvider(
  TimeProvider timeProvider,
  IDbContextFactory<AppDb> dbContextFactory,
  IServiceScopeFactory scopeFactory,
  IPasswordHasher<string> passwordHasher,
  ILogger<LogonTokenProvider> logger) : ILogonTokenProvider
{
  // Remote control is deliberately absent. A token minted with no scopes is a viewer credential,
  // and controlling a device has to be asked for by name.
  private static readonly string[] _defaultDeviceAccessPermissions =
  [
    PermissionNames.DeviceRead,
    PermissionNames.DeviceOverviewRead
  ];

  private readonly IDbContextFactory<AppDb> _dbContextFactory = dbContextFactory;
  private readonly ILogger<LogonTokenProvider> _logger = logger;
  private readonly IPasswordHasher<string> _passwordHasher = passwordHasher;
  private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task<HttpResult<LogonTokenResult>> CreateToken(
    Guid deviceId,
    Guid tenantId,
    Guid userId,
    int expirationMinutes = 5,
    string? userCorrelationId = null,
    string? sessionCorrelationId = null,
    bool writeBaselineGrants = true,
    IReadOnlyList<int>? allowedDesktopSessionIds = null,
    int sessionExpirationMinutes = DtoLimits.SessionExpirationMinutesDefault,
    CancellationToken cancellationToken = default)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    var userExists = await dbContext.Users
      .Where(u => u.Id == userId && u.TenantId == tenantId)
      .AnyAsync(cancellationToken: cancellationToken);

    if (!userExists)
    {
      return HttpResult.Fail<LogonTokenResult>(HttpResultErrorCode.NotFound, $"User {userId} not found in tenant {tenantId}.");
    }

    var now = _timeProvider.GetUtcNow();
    var expiresAt = now.AddMinutes(expirationMinutes);
    var plainTextKey = RandomGenerator.CreateAccessToken();
    var hashedKey = _passwordHasher.HashPassword(string.Empty, plainTextKey);
    var tokenId = Guid.NewGuid();

    var logonToken = new LogonToken
    {
      Id = tokenId,
      Token = hashedKey,
      Prefix = plainTextKey[..8],
      DeviceId = deviceId,
      TenantId = tenantId,
      UserId = userId,
      ExpiresAt = expiresAt,
      UserCorrelationId = userCorrelationId,
      SessionCorrelationId = sessionCorrelationId,
      SessionExpirationMinutes = sessionExpirationMinutes,
      AllowedDesktopSessionIds = allowedDesktopSessionIds
    };

    dbContext.LogonTokens.Add(logonToken);

    // Baseline grants need no per-permission validation; DeviceLogonTokenCreate conveys them.
    // The explicit-scope path (in LogonTokenScopeService) validates each requested permission.
    if (writeBaselineGrants)
    {
      foreach (var permissionName in _defaultDeviceAccessPermissions)
      {
        dbContext.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
          PermissionPrincipalKind.LogonToken,
          tokenId,
          permissionName,
          PermissionScopeKind.Device,
          deviceId,
          tenantId,
          createdBy: null));
      }
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    var hexId = Convert.ToHexString(logonToken.Id.ToByteArray());
    var combinedToken = $"{hexId}:{plainTextKey}";

    _logger.LogInformation(
      "Created logon token for user {UserId} on device {DeviceId} in tenant {TenantId}, expires at {ExpiresAt}",
      userId, deviceId, tenantId, expiresAt);

    return HttpResult.Ok(new LogonTokenResult(
      combinedToken, logonToken.Id, deviceId, tenantId, userId, expiresAt, sessionCorrelationId, userCorrelationId));
  }

  public async Task<HttpResult<LogonTokenResult>> CreateTokenForExternal(
    Guid deviceId,
    Guid tenantId,
    string userCorrelationId,
    int expirationMinutes = 5,
    string? userDisplayName = null,
    string? sessionCorrelationId = null,
    bool writeBaselineGrants = true,
    IReadOnlyList<int>? allowedDesktopSessionIds = null,
    int sessionExpirationMinutes = DtoLimits.SessionExpirationMinutesDefault,
    CancellationToken cancellationToken = default)
  {
    using var scope = _scopeFactory.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var preferencesManager = scope.ServiceProvider.GetRequiredService<IUserPreferencesManager>();

    var username = $"ext-{userCorrelationId.Trim()}";
    var guestUser = await userManager.Users
      .FirstOrDefaultAsync(u => u.UserName == username && u.TenantId == tenantId, cancellationToken: cancellationToken);

    if (guestUser is not null && guestUser.AccountType != AccountType.ExternalUser)
    {
      return HttpResult.Fail<LogonTokenResult>(HttpResultErrorCode.BadRequest,
        $"Username '{username}' is already in use by a non-external account.");
    }

    if (guestUser is null)
    {
      guestUser = new AppUser
      {
        UserName = username,
        Email = $"{username}@controlr.local",
        TenantId = tenantId,
        AccountType = AccountType.ExternalUser
      };
      var createResult = await userManager.CreateAsync(guestUser);
      if (!createResult.Succeeded)
      {
        return HttpResult.Fail<LogonTokenResult>(HttpResultErrorCode.InternalServerError, $"Failed to create external user for correlation ID '{userCorrelationId}'.");
      }
    }

    try
    {
      guestUser.LastLogin = _timeProvider.GetUtcNow();
      await userManager.UpdateAsync(guestUser);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to update LastLogin for external user {UserId}.", guestUser.Id);
      return HttpResult.Fail<LogonTokenResult>(HttpResultErrorCode.InternalServerError,
        "Failed to prepare external user for token issuance.");
    }

    if (!string.IsNullOrWhiteSpace(userDisplayName))
    {
      var preferenceResult = await preferencesManager.SetPreference(
        guestUser.Id,
        new InternalDtos.UserPreferenceRequestDto(UserPreferenceNames.UserDisplayName, userDisplayName.Trim()),
        cancellationToken);

      if (!preferenceResult.IsSuccess)
      {
        _logger.LogWarning(
          "Failed to set UserDisplayName preference for external user {UserId}. Reason: {Reason}",
          guestUser.Id, preferenceResult.Reason);
      }
    }

    return await CreateToken(
      deviceId,
      tenantId,
      guestUser.Id,
      expirationMinutes,
      userCorrelationId,
      sessionCorrelationId,
      writeBaselineGrants,
      allowedDesktopSessionIds,
      sessionExpirationMinutes,
      cancellationToken);
  }

  public Task<LogonTokenValidationResult> ValidateAndConsumeToken(string token, Guid deviceId, CancellationToken cancellationToken = default) =>
    ValidateCore(token, deviceId, consume: true, cancellationToken);

  public Task<LogonTokenValidationResult> ValidateToken(string token, CancellationToken cancellationToken = default) =>
    ValidateCore(token, expectedDeviceId: null, consume: false, cancellationToken);

  private static (Guid TokenId, string Secret)? TryParseToken(string token)
  {
    var parts = token.Split(':', 2);
    if (parts.Length != 2)
    {
      return null;
    }

    try
    {
      var tokenIdBytes = Convert.FromHexString(parts[0]);
      return (new Guid(tokenIdBytes), parts[1]);
    }
    catch (FormatException)
    {
      return null;
    }
  }

  private async Task<Guid?> GetValidUserId(LogonToken logonToken, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    var userId = await dbContext.Users
      .AsNoTracking()
      .Where(u => u.Id == logonToken.UserId && u.TenantId == logonToken.TenantId)
      .Select(u => (Guid?)u.Id)
      .FirstOrDefaultAsync(cancellationToken);

    if (userId is null)
    {
      _logger.LogWarning(
        "User {UserId} not found in tenant {TenantId} for logon token",
        logonToken.UserId, logonToken.TenantId);
    }

    return userId;
  }

  private async Task<LogonTokenValidationResult> ValidateCore(
    string token,
    Guid? expectedDeviceId,
    bool consume,
    CancellationToken cancellationToken)
  {
    try
    {
      var parseResult = TryParseToken(token);
      if (parseResult is not (var tokenId, var secret))
      {
        return LogonTokenValidationResult.Failure("Invalid logon token format.");
      }

      await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

      var logonToken = await dbContext.LogonTokens
        .FirstOrDefaultAsync(x => x.Id == tokenId, cancellationToken);

      if (logonToken is null)
      {
        _logger.LogWarning("Logon token not found.");
        return LogonTokenValidationResult.Failure("Invalid or expired token.");
      }

      if (_timeProvider.GetUtcNow() > logonToken.ExpiresAt)
      {
        _logger.LogWarning("Token has expired at {ExpiresAt}.", logonToken.ExpiresAt);
        return LogonTokenValidationResult.Failure("Token has expired.");
      }

      if (logonToken.IsConsumed)
      {
        return LogonTokenValidationResult.Failure("Token has already been used.");
      }

      if (expectedDeviceId.HasValue && logonToken.DeviceId != expectedDeviceId.Value)
      {
        _logger.LogWarning(
          "Device ID mismatch for logon token. Expected: {ExpectedDeviceId}, Actual: {ActualDeviceId}.",
          expectedDeviceId.Value, logonToken.DeviceId);
        return LogonTokenValidationResult.Failure("Token is not valid for this device.");
      }

      var verification = _passwordHasher.VerifyHashedPassword(string.Empty, logonToken.Token, secret);
      if (verification == PasswordVerificationResult.Failed)
      {
        return LogonTokenValidationResult.Failure("Invalid or expired token.");
      }

      var userId = await GetValidUserId(logonToken, cancellationToken);
      if (userId is null)
      {
        return LogonTokenValidationResult.Failure("User not found.");
      }

      if (consume)
      {
        var consumedCount = await dbContext.LogonTokens
          .Where(x => x.Id == logonToken.Id && !x.IsConsumed)
          .ExecuteUpdateCompatAsync(
            dbContext,
            q => q.ExecuteUpdateAsync(s => s.SetProperty(x => x.IsConsumed, true), cancellationToken),
            x => x.IsConsumed = true,
            cancellationToken);

        if (consumedCount == 0)
        {
          return LogonTokenValidationResult.Failure("Token has already been used.");
        }

        dbContext.LogonTokens.Remove(logonToken);
        await dbContext.SaveChangesAsync(cancellationToken);
      }

      _logger.LogInformation(
        "Validated logon token for user {UserId} on device {DeviceId}.",
        userId, logonToken.DeviceId);

      return LogonTokenValidationResult.Success(
        logonToken.Id,
        userId.Value,
        logonToken.TenantId,
        logonToken.ExpiresAt,
        logonToken.SessionCorrelationId,
        logonToken.AllowedDesktopSessionIds,
        logonToken.SessionExpirationMinutes);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to validate logon token.");
      return LogonTokenValidationResult.Failure("Token validation failed.");
    }
  }
}
