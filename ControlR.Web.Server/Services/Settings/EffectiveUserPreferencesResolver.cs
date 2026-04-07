using ControlR.Libraries.Api.Contracts.Settings;

namespace ControlR.Web.Server.Services.Settings;

public interface IEffectiveUserPreferencesResolver
{
  Task<bool> GetNotifyUserOnSessionStart(
    Guid tenantId,
    Guid userId,
    CancellationToken cancellationToken = default);
}

internal sealed class EffectiveUserPreferencesResolver(
  AppDb appDb,
  ILogger<EffectiveUserPreferencesResolver> logger) : IEffectiveUserPreferencesResolver
{
  private readonly AppDb _appDb = appDb;
  private readonly ILogger<EffectiveUserPreferencesResolver> _logger = logger;

  public async Task<bool> GetNotifyUserOnSessionStart(
    Guid tenantId,
    Guid userId,
    CancellationToken cancellationToken = default)
  {
    return await ResolveBoolean(
      EffectivePreferenceDefinitions.NotifyUserOnSessionStart,
      tenantId,
      userId,
      cancellationToken);
  }

  private async Task<bool> ResolveBoolean(
    EffectivePreferenceDefinition<bool> definition,
    Guid tenantId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    if (!string.IsNullOrWhiteSpace(definition.TenantSettingName))
    {
      var tenantSettingValue = await _appDb.TenantSettings
        .AsNoTracking()
        .Where(x => x.TenantId == tenantId && x.Name == definition.TenantSettingName)
        .Select(x => x.Value)
        .FirstOrDefaultAsync(cancellationToken);

      if (bool.TryParse(tenantSettingValue, out var tenantValue))
      {
        return tenantValue;
      }

      if (!string.IsNullOrWhiteSpace(tenantSettingValue))
      {
        _logger.LogWarning(
          "Failed to parse tenant setting {SettingName} for tenant {TenantId}. Value: {SettingValue}",
          definition.TenantSettingName,
          tenantId,
          tenantSettingValue);
      }
    }

    var userPreferenceValue = await _appDb.UserPreferences
      .AsNoTracking()
      .Where(x => x.UserId == userId && x.Name == definition.UserPreferenceName)
      .Select(x => x.Value)
      .FirstOrDefaultAsync(cancellationToken);

    if (bool.TryParse(userPreferenceValue, out var userValue))
    {
      return userValue;
    }

    if (!string.IsNullOrWhiteSpace(userPreferenceValue))
    {
      _logger.LogWarning(
        "Failed to parse user preference {PreferenceName} for user {UserId}. Value: {PreferenceValue}",
        definition.UserPreferenceName,
        userId,
        userPreferenceValue);
    }

    return definition.DefaultValue;
  }
}