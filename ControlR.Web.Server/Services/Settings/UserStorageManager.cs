using System.Text.RegularExpressions;
using ControlR.Web.Server.Data.Extensions;

namespace ControlR.Web.Server.Services.Settings;

public interface IUserStorageManager
{
  Task<bool> Delete(string key, Guid userId, CancellationToken cancellationToken);
  Task<UserStorageResponseDto?> Get(string key, Guid userId, CancellationToken cancellationToken);
  Task<UserStorageResponseDto> Set(string key, string value, Guid userId, CancellationToken cancellationToken);
}

/// <summary>
///   Manages user-scoped key-value storage. Tenant and user isolation is handled by
///   <see cref="AppDb"/>'s EF Core query filters, not by explicit checks in this class.
/// </summary>
public partial class UserStorageManager(
  IDbContextFactory<AppDb> dbFactory,
  ILogger<UserStorageManager> logger) : IUserStorageManager
{
  private readonly IDbContextFactory<AppDb> _dbFactory = dbFactory;
  private readonly ILogger<UserStorageManager> _logger = logger;

  public async Task<bool> Delete(string key, Guid userId, CancellationToken cancellationToken)
  {
    ValidateKey(key);
    await using var appDb = await _dbFactory.CreateDbContextAsync(cancellationToken);

    var entity = await appDb.UserStorageItems
      .FirstOrDefaultAsync(x => x.Key == key && x.UserId == userId, cancellationToken);

    if (entity is null)
    {
      _logger.LogInformation("User storage key '{Key}' not found for user '{UserId}'.", key, userId);
      return false;
    }

    appDb.UserStorageItems.Remove(entity);
    await appDb.SaveChangesAsync(cancellationToken);
    _logger.LogInformation("User storage key '{Key}' deleted for user '{UserId}'.", key, userId);
    return true;
  }

  public async Task<UserStorageResponseDto?> Get(string key, Guid userId, CancellationToken cancellationToken)
  {
    ValidateKey(key);
    await using var appDb = await _dbFactory.CreateDbContextAsync(cancellationToken);

    var entity = await appDb.UserStorageItems
      .AsNoTracking()
      .Where(x => x.Key == key && x.UserId == userId)
      .Select(x => new UserStorageResponseDto(x.Key, x.Value))
      .FirstOrDefaultAsync(cancellationToken);

    return entity;
  }

  public async Task<UserStorageResponseDto> Set(
    string key, string value, Guid userId, CancellationToken cancellationToken)
  {
    ValidateKey(key);
    ValidateValue(value);
    await using var appDb = await _dbFactory.CreateDbContextAsync(cancellationToken);

    var entity = new UserStorageItem
    {
      Key = key,
      UserId = userId,
      Value = value
    };

    await appDb.UpsertAsync(entity, [x => x.Key, x => x.UserId], cancellationToken);

    _logger.LogInformation("User storage key '{Key}' set for user '{UserId}'.", key, userId);

    return new UserStorageResponseDto(key, value);
  }

  [GeneratedRegex("^[a-zA-Z0-9-]+$")]
  private static partial Regex KeyRegex();

  private static void ValidateKey(string key)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(key);
    if (!KeyRegex().IsMatch(key))
    {
      throw new ArgumentException($"Storage key '{key}' contains invalid characters. Only letters, numbers, and hyphens are allowed.");
    }
  }

  private static void ValidateValue(string value)
  {
    ArgumentNullException.ThrowIfNull(value);
    if (value.Length > UserStorageItem.MaxValueLength)
    {
      throw new ArgumentException($"Storage value exceeds maximum length of {UserStorageItem.MaxValueLength} characters.");
    }
  }
}
