using System.Diagnostics.CodeAnalysis;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Data.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using ControlR.Web.Server.Constants;

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
  IDistributedLock distributedLock,
  IDbContextFactory<AppDb> dbContextFactory,
  ILogger<LogonTokenProvider> logger,
  UserManager<AppUser> userManager) : ILogonTokenProvider
{
  private readonly IMemoryCache _cache = cache;
  private readonly IDbContextFactory<AppDb> _dbContextFactory = dbContextFactory;
  private readonly IDistributedLock _distributedLock = distributedLock;
  private readonly ILogger<LogonTokenProvider> _logger = logger;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly UserManager<AppUser> _userManager = userManager;

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

    var token = RandomGenerator.CreateAccessToken();
    var now = _timeProvider.GetUtcNow();
    var expiresAt = now.AddMinutes(expirationMinutes);

    var logonToken = new LogonTokenModel
    {
      Token = token,
      DeviceId = deviceId,
      ExpiresAt = expiresAt,
      UserId = userId,
      TenantId = tenantId,
      CreatedAt = now,
      IsConsumed = false
    };

    var cacheKey = GetCacheKey(token);
    _cache.Set(cacheKey, logonToken, expiresAt.DateTime);

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
    var username = $"ext-{userCorrelationId.Trim()}";
    var guestUser = await _userManager.Users
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
      var createResult = await _userManager.CreateAsync(guestUser);
      if (!createResult.Succeeded)
      {
        return HttpResult.Fail<LogonTokenModel>(HttpResultErrorCode.InternalServerError, $"Failed to create external user for correlation ID '{userCorrelationId}'.");
      }
    }

    await _userManager.UpdateLastLogin(guestUser);

    return await CreateToken(deviceId, tenantId, guestUser.Id, expirationMinutes, cancellationToken);
  }

  public async Task<LogonTokenValidationResult> ValidateAndConsumeToken(string token, Guid deviceId, CancellationToken cancellationToken = default)
  {
    var lockKey = DistributedLockKeys.GetLogonTokenKey(token);
    using var heldLock  = await _distributedLock.AcquireLock(lockKey, cancellationToken);

    var validationResult = await ValidateTokenInternal(token);
    if (!validationResult.IsSuccess)
    {
      return LogonTokenValidationResult.Failure(validationResult.ErrorMessage ?? "Token validation failed.");
    }

    var logonToken = validationResult.Token;

    if (logonToken.DeviceId != deviceId)
    {
      _logger.LogWarning(
        "Device ID mismatch for logon token. Expected: {ExpectedDeviceId}, Actual: {ActualDeviceId}",
        logonToken.DeviceId, deviceId);
      return LogonTokenValidationResult.Failure("Token is not valid for this device");
    }

    if (logonToken.IsConsumed)
    {
      return LogonTokenValidationResult.Failure("Token has already been used");
    }

    logonToken.IsConsumed = true;
    var cacheKey = GetCacheKey(token);
    _cache.Set(cacheKey, logonToken, logonToken.ExpiresAt.DateTime);

    _logger.LogInformation(
      "Validated and consumed logon token for user {UserId} on device {DeviceId}",
      logonToken.UserId, deviceId);

    return LogonTokenValidationResult.Success(
      validationResult.User.Id,
      logonToken.TenantId,
      validationResult.User.UserName,
      validationResult.User.UserName,
      validationResult.User.Email);
  }

  public async Task<Result<LogonTokenValidationResult>> ValidateToken(string token, CancellationToken cancellationToken = default)
  {
    try
    {
      var validationResult = await ValidateTokenInternal(token);
      if (!validationResult.IsSuccess)
      {
        return Result.Fail<LogonTokenValidationResult>(validationResult.ErrorMessage!);
      }

      _logger.LogInformation(
        "Validated logon token for user {UserId}",
        validationResult.User.Id);

      var result = LogonTokenValidationResult.Success(
        validationResult.User.Id,
        validationResult.Token.TenantId,
        validationResult.User.UserName,
        validationResult.User.UserName,
        validationResult.User.Email);

      return Result.Ok(result);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to validate token");
      return Result.Fail<LogonTokenValidationResult>("Token validation failed");
    }
  }

  private static string GetCacheKey(string token) => $"logon_token:{token}";

  private async Task<TokenValidationResult> ValidateTokenInternal(string token)
  {
    var cacheKey = GetCacheKey(token);

    if (!_cache.TryGetValue(cacheKey, out LogonTokenModel? logonToken) || logonToken is null)
    {
      _logger.LogWarning("Logon token not found.");
      return new TokenValidationResult(false, "Invalid or expired token", null, null);
    }

    if (logonToken.IsConsumed)
    {
      _logger.LogWarning("Token has already been consumed.");
      return new TokenValidationResult(false, "Token has already been used", logonToken, null);
    }

    var now = _timeProvider.GetUtcNow();
    if (now > logonToken.ExpiresAt)
    {
      _logger.LogWarning("Token has expired at {ExpiresAt}", logonToken.ExpiresAt);
      _cache.Remove(cacheKey);
      return new TokenValidationResult(false, "Token has expired", logonToken, null);
    }

    await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
    var user = await dbContext.Users
      .Where(u => u.Id == logonToken.UserId && u.TenantId == logonToken.TenantId)
      .Select(u => new { u.Id, u.UserName, u.Email })
      .FirstOrDefaultAsync();

    if (user is null)
    {
      _logger.LogWarning(
        "User {UserId} not found in tenant {TenantId} for logon token",
        logonToken.UserId, logonToken.TenantId);
      return new TokenValidationResult(false, "User not found.", logonToken, null);
    }

    var userInfo = new UserInfo(user.Id, user.UserName, user.Email);
    return new TokenValidationResult(true, null, logonToken, userInfo);
  }

  private record UserInfo(Guid Id, string? UserName, string? Email);

  private class TokenValidationResult(
    bool isSuccess, 
    string? errorMessage, 
    LogonTokenModel? token, 
    UserInfo? user)
  {

    public string? ErrorMessage { get; } = errorMessage;

    [MemberNotNullWhen(true, nameof(Token), nameof(User))]
    public bool IsSuccess { get; } = isSuccess;
    public LogonTokenModel? Token { get; } = token;

    public UserInfo? User { get; } = user;
  }
}
