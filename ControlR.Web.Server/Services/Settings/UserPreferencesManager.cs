using ControlR.Web.Client.Services;
using ControlR.Libraries.Api.Contracts.Settings;
using ControlR.Web.Server.Data.Extensions;
using ControlR.Web.Server.Primitives;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControlR.Web.Server.Services.Settings;

public interface IUserPreferencesManager : IUserPreferencesProvider
{
  Task<InternalDtos.UserPreferencesDto> GetAllPreferences(
    Guid userId,
    CancellationToken cancellationToken = default);
  Task<HttpResult<InternalDtos.UserPreferenceResponseDto>> SetPreference(
    Guid userId,
    InternalDtos.UserPreferenceRequestDto preference,
    CancellationToken cancellationToken = default);
  Task<HttpResult<InternalDtos.UserPreferencesDto>> SetPreferences(
    Guid userId,
    InternalDtos.UserPreferencesDto preferences,
    CancellationToken cancellationToken = default);
}

public class UserPreferencesManager(
  AuthenticationStateProvider authStateProvider,
  IDbContextFactory<AppDb> dbFactory,
  ILogger<UserPreferencesManager> logger) : IUserPreferencesManager
{
  private readonly AuthenticationStateProvider _authStateProvider = authStateProvider;
  private readonly IDbContextFactory<AppDb> _dbFactory = dbFactory;
  private readonly ILogger<UserPreferencesManager> _logger = logger;

  public async Task<InternalDtos.UserPreferencesDto> GetAllPreferences(
    Guid userId,
    CancellationToken cancellationToken = default)
  {
    await using var appDb = await _dbFactory.CreateDbContextAsync(cancellationToken);

    var values = await appDb.UserPreferences
      .AsNoTracking()
      .Where(x => x.UserId == userId)
      .ToDictionaryAsync(x => x.Name, x => x.Value, cancellationToken);

    return UserPreferenceDefinitions.CreateDto(values, (name, value) =>
      _logger.LogWarning(
        "Failed to parse user preference {PreferenceName} for user {UserId}. Value: {PreferenceValue}",
        name,
        userId,
        value));
  }

  public async Task<InternalDtos.UserPreferencesDto> GetPreferences()
  {
    var authState = await _authStateProvider.GetAuthenticationStateAsync();
    if (!authState.User.TryGetUserId(out var userId) || !await _authStateProvider.IsAuthenticated())
    {
      Dictionary<string, string> values = [];
      return UserPreferenceDefinitions.CreateDto(values);
    }

    return await GetAllPreferences(userId);
  }

  public async Task<HttpResult<InternalDtos.UserPreferenceResponseDto>> SetPreference(
    Guid userId,
    InternalDtos.UserPreferenceRequestDto preference,
    CancellationToken cancellationToken = default)
  {
    await using var appDb = await _dbFactory.CreateDbContextAsync(cancellationToken);
    var user = await appDb.Users
      .Include(x => x.UserPreferences)
      .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

    if (user is null)
    {
      return HttpResult.Fail<InternalDtos.UserPreferenceResponseDto>(HttpResultErrorCode.NotFound, "User not found.");
    }

    user.UserPreferences ??= [];

    if (string.IsNullOrWhiteSpace(preference.Value))
    {
      var existingPreferenceToDelete = user.UserPreferences.FirstOrDefault(x => x.Name == preference.Name);
      if (existingPreferenceToDelete is not null)
      {
        user.UserPreferences.Remove(existingPreferenceToDelete);
        await appDb.SaveChangesAsync(cancellationToken);
      }

      return HttpResult.Ok(new InternalDtos.UserPreferenceResponseDto(null, preference.Name, null));
    }

    var normalizationResult = UserPreferenceDefinitions.Normalize(preference.Name, preference.Value);
    if (!normalizationResult.IsSuccess)
    {
      _logger.LogError(
        "Failed to normalize preference value for {PreferenceName}. Reason: {Reason}",
        preference.Name,
        normalizationResult.ErrorMessage);

      return HttpResult.Fail<InternalDtos.UserPreferenceResponseDto>(
        HttpResultErrorCode.ValidationFailed,
        normalizationResult.ErrorMessage ?? "Preference value is invalid.");
    }

    var entity = new UserPreference
    {
      Id = Guid.NewGuid(),
      Name = preference.Name,
      UserId = userId,
      Value = normalizationResult.Value ?? string.Empty
    };

    await appDb.UpsertAsync(entity, [x => x.Name, x => x.UserId], cancellationToken);

    var savedPreference = await appDb.UserPreferences
      .AsNoTracking()
      .SingleAsync(x => x.UserId == userId && x.Name == preference.Name, cancellationToken);

    return HttpResult.Ok(savedPreference.ToInternalResponseDto());
  }

  public async Task SetPreference<T>(string preferenceName, T value)
  {
    var authState = await _authStateProvider.GetAuthenticationStateAsync();
    if (!authState.User.TryGetUserId(out var userId) || !await _authStateProvider.IsAuthenticated())
    {
      _logger.LogWarning("Cannot set preference {PreferenceName} - user is not authenticated.", preferenceName);
      return;
    }

    var stringValue = UserPreferenceDefinitions.FormatValue(preferenceName, value)?.Trim();
    if (string.IsNullOrWhiteSpace(stringValue))
    {
      _logger.LogWarning("Cannot set preference {PreferenceName} - value is null or empty.", preferenceName);
      return;
    }

    var result = await SetPreference(userId, new InternalDtos.UserPreferenceRequestDto(preferenceName, stringValue));
    if (!result.IsSuccess)
    {
      _logger.LogError(
        "Failed to set preference {PreferenceName}. Reason: {Reason}",
        preferenceName,
        result.Reason);
    }
  }

  public async Task<HttpResult<InternalDtos.UserPreferencesDto>> SetPreferences(
    Guid userId,
    InternalDtos.UserPreferencesDto preferences,
    CancellationToken cancellationToken = default)
  {
    await using var appDb = await _dbFactory.CreateDbContextAsync(cancellationToken);

    var user = await appDb.Users
      .Include(x => x.UserPreferences)
      .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

    if (user is null)
    {
      return HttpResult.Fail<InternalDtos.UserPreferencesDto>(HttpResultErrorCode.NotFound, "User not found.");
    }

    user.UserPreferences ??= [];

    var normalizationResults = new List<(string Name, string? Value)>();
    foreach (var preference in UserPreferenceDefinitions.GetValues(preferences).Select(x => new InternalDtos.UserPreferenceRequestDto(x.Name, x.Value ?? string.Empty)))
    {
      if (string.IsNullOrWhiteSpace(preference.Value))
      {
        normalizationResults.Add((preference.Name, null));
        continue;
      }

      var normalizationResult = UserPreferenceDefinitions.Normalize(preference.Name, preference.Value);
      if (!normalizationResult.IsSuccess)
      {
        _logger.LogError(
          "Failed to normalize preference value for {PreferenceName}. Reason: {Reason}",
          preference.Name,
          normalizationResult.ErrorMessage);

        return HttpResult.Fail<InternalDtos.UserPreferencesDto>(
          HttpResultErrorCode.ValidationFailed,
          normalizationResult.ErrorMessage ?? "Preference value is invalid.");
      }

      normalizationResults.Add((preference.Name, normalizationResult.Value ?? string.Empty));
    }

    foreach (var result in normalizationResults)
    {
      var existingPreference = user.UserPreferences.FirstOrDefault(x => x.Name == result.Name);
      if (result.Value is null)
      {
        if (existingPreference is not null)
        {
          user.UserPreferences.Remove(existingPreference);
        }

        continue;
      }

      if (existingPreference is not null)
      {
        existingPreference.Value = result.Value;
        continue;
      }

      user.UserPreferences.Add(new UserPreference
      {
        Name = result.Name,
        UserId = userId,
        Value = result.Value
      });
    }

    await appDb.SaveChangesAsync(cancellationToken);
    return HttpResult.Ok(await GetAllPreferences(userId, cancellationToken));
  }
}
