using System.Diagnostics.CodeAnalysis;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Web.Server.Services.LogonTokens;

public interface ILogonTokenProvider
{
  Task<Result<LogonTokenCreationResult>> CreateTemporaryUserAsync(Guid deviceId, string displayName);

  Task<LogonTokenModel> CreateTokenAsync(
    Guid deviceId,
    Guid tenantId,
    int expirationMinutes = 15,
    string? userIdentifier = null,
    string? displayName = null,
    string? email = null);

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

  public async Task<Result<LogonTokenCreationResult>> CreateTemporaryUserAsync(Guid deviceId, string displayName)
  {
    try
    {
      var tenantId = Guid.Empty; // System tenant
      var expiresAt = _timeProvider.GetUtcNow().AddHours(24);

      var userId = await CreateTemporaryUserAsync(tenantId, displayName, null, expiresAt);
      var tokenModel = await CreateTokenAsync(deviceId, tenantId, 15 * 60, userId.ToString(), displayName);

      return Result.Ok(new LogonTokenCreationResult(tokenModel.Token, userId));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to create temporary user for device {DeviceId}", deviceId);
      return Result.Fail<LogonTokenCreationResult>("Failed to create temporary user");
    }
  }

  public async Task<LogonTokenModel> CreateTokenAsync(
    Guid deviceId,
    Guid tenantId,
    int expirationMinutes = 15,
    string? userIdentifier = null,
    string? displayName = null,
    string? email = null)
  {
    var token = RandomGenerator.CreateAccessToken();
    var now = _timeProvider.GetUtcNow();
    var expiresAt = now.AddMinutes(expirationMinutes);

    // If no real user specified, create a temporary user
    if (string.IsNullOrWhiteSpace(userIdentifier))
    {
      var tempUserId = await CreateTemporaryUserAsync(tenantId, displayName, email, expiresAt);
      userIdentifier = tempUserId.ToString();
    }

    var logonToken = new LogonTokenModel
    {
      Token = token,
      DeviceId = deviceId,
      ExpiresAt = expiresAt,
      UserIdentifier = userIdentifier,
      DisplayName = displayName,
      Email = email,
      TenantId = tenantId,
      CreatedAt = now,
      IsConsumed = false
    };

    // Store in cache with expiration
    var cacheKey = GetCacheKey(token);
    _cache.Set(cacheKey, logonToken, expiresAt.DateTime);

    _logger.LogInformation(
      "Created logon token for device {DeviceId} in tenant {TenantId}, expires at {ExpiresAt}",
      deviceId, tenantId, expiresAt);

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
      "Successfully validated and consumed logon token for user {UserId} (temporary: {IsTemporary}) on device {DeviceId}",
      validationResult.User.Id, validationResult.User.IsTemporary, deviceId);

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
        "Successfully validated logon token for user {UserId} (temporary: {IsTemporary})",
        validationResult.User.Id, validationResult.User.IsTemporary);

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

  private async Task<Guid> CreateTemporaryUserAsync(
    Guid tenantId,
    string? displayName,
    string? email,
    DateTimeOffset expiresAt)
  {
    using var dbContext = await _dbContextFactory.CreateDbContextAsync();

    var tempUserId = Guid.NewGuid();
    var tempUserName = $"temp_user_{tempUserId:N}";

    var tempUser = new AppUser
    {
      Id = tempUserId,
      UserName = tempUserName,
      NormalizedUserName = tempUserName.ToUpperInvariant(),
      Email = email,
      NormalizedEmail = email?.ToUpperInvariant(),
      TenantId = tenantId,
      IsTemporary = true,
      TemporaryUserExpiresAt = expiresAt,
      SecurityStamp = Guid.NewGuid().ToString(),
      ConcurrencyStamp = Guid.NewGuid().ToString(),
      EmailConfirmed = false,
      PhoneNumberConfirmed = false,
      TwoFactorEnabled = false,
      LockoutEnabled = false,
      AccessFailedCount = 0
    };

    dbContext.Users.Add(tempUser);

    // Add display name as a user preference if provided
    if (!string.IsNullOrWhiteSpace(displayName))
    {
      var displayNamePreference = new UserPreference
      {
        UserId = tempUserId,
        TenantId = tenantId,
        Name = UserPreferenceNames.UserDisplayName,
        Value = displayName
      };
      dbContext.UserPreferences.Add(displayNamePreference);
    }

    await dbContext.SaveChangesAsync();

    _logger.LogInformation(
      "Created temporary user {UserId} for tenant {TenantId}, expires at {ExpiresAt}",
      tempUserId, tenantId, expiresAt);

    return tempUserId;
  }
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

    // Validate the user exists (either real or temporary)
    if (string.IsNullOrWhiteSpace(logonToken.UserIdentifier))
    {
      _logger.LogWarning("Logon token missing user identifier: {Token}", token);
      return new TokenValidationResult(false, "Invalid token configuration", logonToken, null);
    }

    using var dbContext = await _dbContextFactory.CreateDbContextAsync();
    var userGuid = Guid.Parse(logonToken.UserIdentifier);
    var user = await dbContext.Users
      .Where(u => u.Id == userGuid && u.TenantId == logonToken.TenantId)
      .Select(u => new { u.Id, u.UserName, u.Email, u.IsTemporary })
      .FirstOrDefaultAsync();

    if (user is null)
    {
      _logger.LogWarning(
        "User {UserId} not found in tenant {TenantId} for logon token",
        logonToken.UserIdentifier, logonToken.TenantId);
      return new TokenValidationResult(false, "User not found", logonToken, null);
    }

    var userInfo = new UserInfo(user.Id, user.UserName, user.Email, user.IsTemporary);
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

  private record UserInfo(Guid Id, string? UserName, string? Email, bool IsTemporary);
}
