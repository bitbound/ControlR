using System.Collections.Frozen;
using ControlR.Web.Server.Primitives;

namespace ControlR.Web.Server.Services.Settings;

public interface ITenantSettingsManager
{
  Task<HttpResult<TenantSettingResponseDto>> SetSetting(
    Guid tenantId,
    TenantSettingRequestDto setting,
    CancellationToken cancellationToken = default);
}

public class TenantSettingsManager(
  AppDb appDb,
  IEnumerable<ITenantSettingValueHandler> handlers,
  ILogger<TenantSettingsManager> logger) : ITenantSettingsManager
{
  private readonly AppDb _appDb = appDb;
  private readonly FrozenDictionary<string, ITenantSettingValueHandler> _handlers = handlers.ToHandlerDictionary();
  private readonly ILogger<TenantSettingsManager> _logger = logger;

  public async Task<HttpResult<TenantSettingResponseDto>> SetSetting(
    Guid tenantId,
    TenantSettingRequestDto setting,
    CancellationToken cancellationToken = default)
  {
    var tenant = await _appDb.Tenants
      .Include(x => x.TenantSettings)
      .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

    if (tenant is null)
    {
      return HttpResult.Fail<TenantSettingResponseDto>(HttpResultErrorCode.NotFound, "Tenant not found.");
    }

    tenant.TenantSettings ??= [];

    if (string.IsNullOrWhiteSpace(setting.Value))
    {
      var existingInstanceIdSetting = tenant.TenantSettings.FirstOrDefault(x => x.Name == setting.Name);
      if (existingInstanceIdSetting is not null)
      {
        tenant.TenantSettings.Remove(existingInstanceIdSetting);
        await _appDb.SaveChangesAsync(cancellationToken);
      }

      return HttpResult.Ok(new TenantSettingResponseDto(null, setting.Name, null));
    }

    var normalizationResult = NormalizeSettingValue(setting);
    if (!normalizationResult.IsSuccess)
    {
      _logger.LogError(
        "Failed to normalize setting value for {SettingName}. Reason: {Reason}", 
        setting.Name, 
        normalizationResult.Reason);

      return normalizationResult.ToHttpResult(new TenantSettingResponseDto(null, setting.Name, null));
    }

    
    var normalizedValue = normalizationResult.Value;

    var existingSetting = tenant.TenantSettings.FirstOrDefault(x => x.Name == setting.Name);
    if (existingSetting is not null)
    {
      existingSetting.Value = normalizedValue;
      await _appDb.SaveChangesAsync(cancellationToken);
      return HttpResult.Ok(existingSetting.ToDto());
    }

    var entity = new TenantSetting
    {
      Name = setting.Name,
      Value = normalizedValue,
      TenantId = tenantId
    };

    tenant.TenantSettings.Add(entity);
    await _appDb.SaveChangesAsync(cancellationToken);
    return HttpResult.Ok(entity.ToDto());
  }

  private HttpResult<string> NormalizeSettingValue(TenantSettingRequestDto setting)
  {
    if (_handlers.TryGetValue(setting.Name, out var handler))
    {
      return handler.ValidateAndNormalize(setting.Value);
    }

    return HttpResult.Ok(setting.Value.Trim());
  }
}