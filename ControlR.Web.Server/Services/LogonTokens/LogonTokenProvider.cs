using System.Diagnostics.CodeAnalysis;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Web.Server.Services.LogonTokens;

public interface ILogonTokenProvider
{
  Task<LogonTokenModel> CreateTokenAsync(
    Guid deviceId,
    Guid tenantId,
    Guid userId,
    int expirationMinutes = 15);

  Task<LogonTokenValidationResult> ValidateAndConsumeTokenAsync(string token, Guid deviceId);
  Task<Result<LogonTokenValidationResult>> ValidateTokenAsync(string token);
}


public class LogonTokenProvider : ILogonTokenProvider
{
  private readonly IMemoryCache _cache;
  private readonly IDbContextFactory<AppDb> _dbContextFactory;
  private readonly ILogger<LogonTokenProvider> _logger;
  private readonly TimeProvider _timeProvider;
  public LogonTokenProvider(
    TimeProvider timeProvider,
    IMemoryCache cache,
    IDbContextFactory<AppDb> dbContextFactory,
    ILogger<LogonTokenProvider> logger)
  {
    _cache = cache;
    _timeProvider = timeProvider;
    _dbContextFactory = dbContextFactory;
    _logger = logger;
  }

  public async Task<LogonTokenModel> CreateTokenAsync(
    Guid deviceId,
    Guid tenantId,
    Guid userId,
    int expirationMinutes = 15)
  {
    // Validate that the user exists and belongs to the tenant
    using var dbContext = await _dbContextFactory.CreateDbContextAsync();
    var user = await dbContext.Users
      .Where(u => u.Id == userId && u.TenantId == tenantId)
      .Select(u => new { u.Id, u.UserName, u.Email })
      .FirstOrDefaultAsync()
      ?? throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
      
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

    // Store in cache with expiration
    var cacheKey = GetCacheKey(token);
    _cache.Set(cacheKey, logonToken, expiresAt.DateTime);

    _logger.LogInformation(
      "Created logon token for user {UserId} on device {DeviceId} in tenant {TenantId}, expires at {ExpiresAt}",
      userId, deviceId, tenantId, expiresAt);

    return logonToken;
  }

  public async Task<LogonTokenValidationResult> ValidateAndConsumeTokenAsync(string token, Guid deviceId)
  {
    var validationResult = await ValidateTokenInternalAsync(token);
    if (!validationResult.IsSuccess)
    {
      return LogonTokenValidationResult.Failure(validationResult.ErrorMessage!);
    }

    var logonToken = validationResult.Token;

    // Check if token matches the device
    if (logonToken.DeviceId != deviceId)
    {
      _logger.LogWarning(
        "Device ID mismatch for token {Token}. Expected: {ExpectedDeviceId}, Actual: {ActualDeviceId}",
        token, logonToken.DeviceId, deviceId);
      return LogonTokenValidationResult.Failure("Token is not valid for this device");
    }

    // Mark token as consumed and update cache
    logonToken.IsConsumed = true;
    var cacheKey = GetCacheKey(token);
    _cache.Set(cacheKey, logonToken, logonToken.ExpiresAt.DateTime);

    _logger.LogInformation(
      "Successfully validated and consumed logon token for user {UserId} on device {DeviceId}",
      logonToken.UserId, deviceId);

    return LogonTokenValidationResult.Success(
      validationResult.User.Id,
      logonToken.TenantId,
      validationResult.User.UserName,
      validationResult.User.UserName,
      validationResult.User.Email);
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
        "Successfully validated logon token for user {UserId}",
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

   private async Task<TokenValidationResult> ValidateTokenInternalAsync(string token)
  {
    var cacheKey = GetCacheKey(token);

    if (!_cache.TryGetValue(cacheKey, out LogonTokenModel? logonToken) || logonToken is null)
    {
      _logger.LogWarning("Logon token not found: {Token}", token);
      return new TokenValidationResult(false, "Invalid or expired token", null, null);
    }

    // Check if token is already consumed
    if (logonToken.IsConsumed)
    {
      _logger.LogWarning("Token has already been consumed: {Token}", token);
      return new TokenValidationResult(false, "Token has already been used", logonToken, null);
    }

    // Check if token is expired
    var now = _timeProvider.GetUtcNow();
    if (now > logonToken.ExpiresAt)
    {
      _logger.LogWarning("Token has expired: {Token}, expired at {ExpiresAt}", token, logonToken.ExpiresAt);
      _cache.Remove(cacheKey);
      return new TokenValidationResult(false, "Token has expired", logonToken, null);
    }

    // Validate the user exists in the database
    using var dbContext = await _dbContextFactory.CreateDbContextAsync();
    var user = await dbContext.Users
      .Where(u => u.Id == logonToken.UserId && u.TenantId == logonToken.TenantId)
      .Select(u => new { u.Id, u.UserName, u.Email })
      .FirstOrDefaultAsync();

    if (user is null)
    {
      _logger.LogWarning(
        "User {UserId} not found in tenant {TenantId} for logon token",
        logonToken.UserId, logonToken.TenantId);
      return new TokenValidationResult(false, "User not found", logonToken, null);
    }

    var userInfo = new UserInfo(user.Id, user.UserName, user.Email);
    return new TokenValidationResult(true, null, logonToken, userInfo);
  }

  private class TokenValidationResult
  {

    public TokenValidationResult(bool isSuccess, string? errorMessage, LogonTokenModel? token, UserInfo? user)
    {
      if (isSuccess)
      {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(user);
      }

      IsSuccess = isSuccess;
      ErrorMessage = errorMessage;
      Token = token;
      User = user;
    }

    public string? ErrorMessage { get; }

    [MemberNotNullWhen(true, nameof(Token), nameof(User))]
    public bool IsSuccess { get; }
    public LogonTokenModel? Token { get; }
    
    public UserInfo? User { get; }
  }

  private record UserInfo(Guid Id, string? UserName, string? Email);
}
