using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Web.Server.Services.LogonTokens;

public interface ILogonTokenProvider
{
  Task<LogonTokenModel> CreateTokenAsync(
    Guid deviceId,
    Guid tenantId,
    Guid? userId,
    LogonTokenKind kind,
    int expirationMinutes = 5);

  Task<LogonTokenValidationResult> ValidateAndConsumeTokenAsync(string token, Guid deviceId);
  Task<Result<LogonTokenValidationResult>> ValidateTokenAsync(string token);
}

public class LogonTokenProvider(
  TimeProvider timeProvider,
  IMemoryCache cache,
  IDbContextFactory<AppDb> dbContextFactory,
  ILogger<LogonTokenProvider> logger) : ILogonTokenProvider
{
  // Per-token async locks to ensure single-consumption race safety
  private static readonly ConcurrentDictionary<string, SemaphoreSlim> _tokenLocks = new();
  private readonly IMemoryCache _cache = cache;
  private readonly IDbContextFactory<AppDb> _dbContextFactory = dbContextFactory;
  private readonly ILogger<LogonTokenProvider> _logger = logger;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task<LogonTokenModel> CreateTokenAsync(
    Guid deviceId,
    Guid tenantId,
    Guid? userId,
    LogonTokenKind kind,
    int expirationMinutes = 5)
  {
    if (kind == LogonTokenKind.User)
    {
      if (!userId.HasValue)
      {
        throw new InvalidOperationException("UserId is required for user logon tokens.");
      }

      await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
      var exists = await dbContext.Users
        .Where(u => u.Id == userId.Value && u.TenantId == tenantId)
        .Select(u => new { u.Id, u.UserName, u.Email })
        .AnyAsync();

      if (!exists)
      {
        throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
      }
    }

    var token = RandomGenerator.CreateAccessToken();
    var now = _timeProvider.GetUtcNow();
    var expiresAt = now.AddMinutes(expirationMinutes);

    var logonToken = new LogonTokenModel
    {
      Token = token,
      DeviceId = deviceId,
      ExpiresAt = expiresAt,
      Kind = kind,
      UserId = userId,
      TenantId = tenantId,
      CreatedAt = now,
      IsConsumed = false
    };

    var cacheKey = GetCacheKey(token);
    _cache.Set(cacheKey, logonToken, expiresAt.DateTime);

    if (kind == LogonTokenKind.User)
    {
      _logger.LogInformation(
        "Created logon token for user {UserId} on device {DeviceId} in tenant {TenantId}, expires at {ExpiresAt}",
        userId, deviceId, tenantId, expiresAt);
    }
    else
    {
      _logger.LogInformation(
        "Created service logon token for device {DeviceId} in tenant {TenantId}, expires at {ExpiresAt}",
        deviceId, tenantId, expiresAt);
    }

    return logonToken;
  }

  public async Task<LogonTokenValidationResult> ValidateAndConsumeTokenAsync(string token, Guid deviceId)
  {
    var semaphore = _tokenLocks.GetOrAdd(token, _ => new SemaphoreSlim(1, 1));
    await semaphore.WaitAsync();
    try
    {
      var validationResult = await ValidateTokenInternalAsync(token);
      if (!validationResult.IsSuccess)
      {
        return LogonTokenValidationResult.Failure(validationResult.ErrorMessage!);
      }

      var logonToken = validationResult.Token;

      if (logonToken.DeviceId != deviceId)
      {
        _logger.LogWarning(
          "Device ID mismatch for token {Token}. Expected: {ExpectedDeviceId}, Actual: {ActualDeviceId}",
          token, logonToken.DeviceId, deviceId);
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
        "Validated and consumed {Kind} logon token for user {UserId} on device {DeviceId}",
        logonToken.Kind, logonToken.UserId, deviceId);

      return LogonTokenValidationResult.Success(
        validationResult.User.Id,
        logonToken.TenantId,
        validationResult.User.UserName,
        validationResult.User.UserName,
        validationResult.User.Email);
    }
    finally
    {
      semaphore.Release();
      if (semaphore.CurrentCount == 1)
      {
        _tokenLocks.TryRemove(token, out _);
      }
    }
  }

  public async Task<Result<LogonTokenValidationResult>> ValidateTokenAsync(string token)
  {
    try
    {
      var validationResult = await ValidateTokenInternalAsync(token);
      if (!validationResult.IsSuccess)
      {
        return Result.Fail<LogonTokenValidationResult>(validationResult.ErrorMessage!);
      }

      _logger.LogInformation(
        "Validated {Kind} logon token for user {UserId}",
        validationResult.Token.Kind, validationResult.User.Id);

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

  private async Task<TokenValidationResult> ValidateTokenInternalAsync(string token)
  {
    var cacheKey = GetCacheKey(token);

    if (!_cache.TryGetValue(cacheKey, out LogonTokenModel? logonToken) || logonToken is null)
    {
      _logger.LogWarning("Logon token not found: {Token}", token);
      return new TokenValidationResult(false, "Invalid or expired token", null, null);
    }

    if (logonToken.IsConsumed)
    {
      _logger.LogWarning("Token has already been consumed: {Token}", token);
      return new TokenValidationResult(false, "Token has already been used", logonToken, null);
    }

    var now = _timeProvider.GetUtcNow();
    if (now > logonToken.ExpiresAt)
    {
      _logger.LogWarning("Token has expired: {Token}, expired at {ExpiresAt}", token, logonToken.ExpiresAt);
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
