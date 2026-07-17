using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Data.Enums;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Web.Server.Services.LogonTokens;

public interface ILogonTokenProvider
{
  Task<HttpResult<LogonTokenModel>> CreateToken(
    Guid deviceId,
    Guid tenantId,
    Guid userId,
    int expirationMinutes = 5,
    CancellationToken cancellationToken = default);

  Task<HttpResult<LogonTokenModel>> CreateTokenForExternal(
    Guid deviceId,
    Guid tenantId,
    string userCorrelationId,
    int expirationMinutes = 5,
    CancellationToken cancellationToken = default);

  Task<LogonTokenValidationResult> ValidateAndConsumeToken(string token, Guid deviceId, CancellationToken cancellationToken = default);
  Task<Result<LogonTokenValidationResult>> ValidateToken(string token, CancellationToken cancellationToken = default);
}

public class LogonTokenProvider(
  TimeProvider timeProvider,
  IMemoryCache cache,
  IDbContextFactory<AppDb> dbContextFactory,
  IServiceScopeFactory scopeFactory,
  ILogger<LogonTokenProvider> logger) : ILogonTokenProvider
{
  private readonly IMemoryCache _cache = cache;
  private readonly IDbContextFactory<AppDb> _dbContextFactory = dbContextFactory;
  private readonly ILogger<LogonTokenProvider> _logger = logger;
  private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task<HttpResult<LogonTokenModel>> CreateToken(
    Guid deviceId,
    Guid tenantId,
    Guid userId,
    int expirationMinutes = 5,
    CancellationToken cancellationToken = default)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    var userExists = await dbContext.Users
      .Where(u => u.Id == userId && u.TenantId == tenantId)
      .AnyAsync(cancellationToken: cancellationToken);

    if (!userExists)
    {
      return HttpResult.Fail<LogonTokenModel>(HttpResultErrorCode.NotFound, $"User {userId} not found in tenant {tenantId}.");
    }

    var now = _timeProvider.GetUtcNow();
    var expiresAt = now.AddMinutes(expirationMinutes);

    var logonToken = new LogonTokenModel
    {
      Token = RandomGenerator.CreateAccessToken(),
      DeviceId = deviceId,
      ExpiresAt = expiresAt,
      UserId = userId,
      TenantId = tenantId,
      CreatedAt = now
    };

    // The absolute expiration lets the cache evict the entry on its own once the token
    // is no longer usable, so entries never accumulate without a background sweeper.
    _cache.Set(GetCacheKey(logonToken.Token), logonToken, expiresAt);

    _logger.LogInformation(
      "Created logon token for user {UserId} on device {DeviceId} in tenant {TenantId}, expires at {ExpiresAt}",
      userId, deviceId, tenantId, expiresAt);

    return HttpResult.Ok(logonToken);
  }

  public async Task<HttpResult<LogonTokenModel>> CreateTokenForExternal(
    Guid deviceId,
    Guid tenantId,
    string userCorrelationId,
    int expirationMinutes = 5,
    CancellationToken cancellationToken = default)
  {
    using var scope = _scopeFactory.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    var username = $"ext-{userCorrelationId.Trim()}";
    var guestUser = await userManager.Users
      .FirstOrDefaultAsync(u => u.UserName == username && u.TenantId == tenantId, cancellationToken: cancellationToken);

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
        return HttpResult.Fail<LogonTokenModel>(HttpResultErrorCode.InternalServerError, $"Failed to create external user for correlation ID '{userCorrelationId}'.");
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
      return HttpResult.Fail<LogonTokenModel>(HttpResultErrorCode.InternalServerError,
        "Failed to prepare external user for token issuance.");
    }

    return await CreateToken(deviceId, tenantId, guestUser.Id, expirationMinutes, cancellationToken);
  }

  public async Task<LogonTokenValidationResult> ValidateAndConsumeToken(string token, Guid deviceId, CancellationToken cancellationToken = default)
  {
    var logonToken = GetLiveToken(token, out var error);
    if (logonToken is null)
    {
      return LogonTokenValidationResult.Failure(error);
    }

    // Device binding is immutable, so it can be checked outside the single-use gate.
    if (logonToken.DeviceId != deviceId)
    {
      _logger.LogWarning(
        "Device ID mismatch for logon token. Expected: {ExpectedDeviceId}, Actual: {ActualDeviceId}.",
        logonToken.DeviceId, deviceId);
      return LogonTokenValidationResult.Failure("Token is not valid for this device.");
    }

    // The single-use gate: exactly one caller wins, lock-free, even under concurrency.
    if (!logonToken.TryConsume())
    {
      return LogonTokenValidationResult.Failure("Token has already been used.");
    }

    // Only the winning caller reaches the database.
    var userId = await GetValidUserId(logonToken, cancellationToken);
    if (userId is null)
    {
      return LogonTokenValidationResult.Failure("User not found.");
    }

    _logger.LogInformation(
      "Validated and consumed logon token for user {UserId} on device {DeviceId}.",
      userId, deviceId);

    return LogonTokenValidationResult.Success(userId.Value, logonToken.TenantId);
  }

  public async Task<Result<LogonTokenValidationResult>> ValidateToken(string token, CancellationToken cancellationToken = default)
  {
    try
    {
      var logonToken = GetLiveToken(token, out var error);
      if (logonToken is null)
      {
        return Result.Fail<LogonTokenValidationResult>(error);
      }

      if (logonToken.IsConsumed)
      {
        return Result.Fail<LogonTokenValidationResult>("Token has already been used.");
      }

      var userId = await GetValidUserId(logonToken, cancellationToken);
      if (userId is null)
      {
        return Result.Fail<LogonTokenValidationResult>("User not found.");
      }

      _logger.LogInformation("Validated logon token for user {UserId}", userId);

      var result = LogonTokenValidationResult.Success(userId.Value, logonToken.TenantId);
      return Result.Ok(result);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to validate token");
      return Result.Fail<LogonTokenValidationResult>("Token validation failed");
    }
  }

  private static string GetCacheKey(string token) => $"logon_token:{token}";

  /// <summary>
  /// Returns the cached token when it exists and has not expired. Expired entries are
  /// evicted eagerly. On failure, <paramref name="error"/> describes the reason.
  /// </summary>
  private LogonTokenModel? GetLiveToken(string token, out string error)
  {
    var cacheKey = GetCacheKey(token);

    if (!_cache.TryGetValue(cacheKey, out LogonTokenModel? logonToken) || logonToken is null)
    {
      _logger.LogWarning("Logon token not found.");
      error = "Invalid or expired token.";
      return null;
    }

    if (_timeProvider.GetUtcNow() > logonToken.ExpiresAt)
    {
      _logger.LogWarning("Token has expired at {ExpiresAt}.", logonToken.ExpiresAt);
      _cache.Remove(cacheKey);
      error = "Token has expired.";
      return null;
    }

    error = string.Empty;
    return logonToken;
  }

  private async Task<Guid?> GetValidUserId(LogonTokenModel logonToken, CancellationToken cancellationToken)
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
}
