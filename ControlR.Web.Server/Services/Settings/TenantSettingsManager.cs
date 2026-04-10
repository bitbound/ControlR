using ControlR.Libraries.Api.Contracts.Settings;
using ControlR.Web.Server.Data.Extensions;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Extensions;

namespace ControlR.Web.Server.Services.Settings;

public interface ITenantSettingsManager
{
  Task<TenantSettingsDto> GetAllSettings(
    Guid tenantId,
    CancellationToken cancellationToken = default);
  Task<HttpResult<TenantSettingResponseDto>> SetSetting(
    Guid tenantId,
    TenantSettingRequestDto setting,
    CancellationToken cancellationToken = default);
  Task<HttpResult<TenantSettingsDto>> SetSettings(
    Guid tenantId,
    TenantSettingsDto settings,
    CancellationToken cancellationToken = default);
}

public class TenantSettingsManager(
  AppDb appDb,
  ILogger<TenantSettingsManager> logger) : ITenantSettingsManager
{
  private readonly AppDb _appDb = appDb;
  private readonly ILogger<TenantSettingsManager> _logger = logger;

  public async Task<TenantSettingsDto> GetAllSettings(
    Guid tenantId,
    CancellationToken cancellationToken = default)
  {
    var values = await _appDb.TenantSettings
      .AsNoTracking()
      .Where(x => x.TenantId == tenantId)
      .ToDictionaryAsync(x => x.Name, x => x.Value, cancellationToken);

    return TenantSettingDefinitions.CreateDto(values, (name, value) =>
      _logger.LogWarning(
        "Failed to parse tenant setting {SettingName} for tenant {TenantId}. Value: {SettingValue}",
        name,
        tenantId,
        value));
  }

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
      var existingSettingToDelete = tenant.TenantSettings.FirstOrDefault(x => x.Name == setting.Name);
      if (existingSettingToDelete is not null)
      {
        tenant.TenantSettings.Remove(existingSettingToDelete);
        await _appDb.SaveChangesAsync(cancellationToken);
      }

      return HttpResult.Ok(new TenantSettingResponseDto(null, setting.Name, null));
    }

    var normalizationResult = TenantSettingDefinitions.Normalize(setting.Name, setting.Value);
    if (!normalizationResult.IsSuccess)
    {
      _logger.LogError(
        "Failed to normalize setting value for {SettingName}. Reason: {Reason}",
        setting.Name,
        normalizationResult.ErrorMessage);

      return HttpResult.Fail<TenantSettingResponseDto>(
        HttpResultErrorCode.ValidationFailed,
        normalizationResult.ErrorMessage ?? "Setting value is invalid.");
    }

    var entity = new TenantSetting
    {
      Id = Guid.NewGuid(),
      Name = setting.Name,
      Value = normalizationResult.Value ?? string.Empty,
      TenantId = tenantId
    };

    await _appDb.UpsertAsync(entity, [x => x.Name, x => x.TenantId], cancellationToken);

    var savedSetting = await _appDb.TenantSettings
      .AsNoTracking()
      .SingleAsync(x => x.TenantId == tenantId && x.Name == setting.Name, cancellationToken);

    return HttpResult.Ok(savedSetting.ToDto());
  }

  public async Task<HttpResult<TenantSettingsDto>> SetSettings(
    Guid tenantId,
    TenantSettingsDto settings,
    CancellationToken cancellationToken = default)
  {
    var tenant = await _appDb.Tenants
      .Include(x => x.TenantSettings)
      .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

    if (tenant is null)
    {
      return HttpResult.Fail<TenantSettingsDto>(HttpResultErrorCode.NotFound, "Tenant not found.");
    }

    tenant.TenantSettings ??= [];

    var normalizationResults = new List<(string Name, string? Value)>();
    foreach (var setting in TenantSettingDefinitions.GetValues(settings).Select(x => new TenantSettingRequestDto(x.Name, x.Value ?? string.Empty)))
    {
      if (string.IsNullOrWhiteSpace(setting.Value))
      {
        normalizationResults.Add((setting.Name, null));
        continue;
      }

      var normalizationResult = TenantSettingDefinitions.Normalize(setting.Name, setting.Value);
      if (!normalizationResult.IsSuccess)
      {
        _logger.LogError(
          "Failed to normalize setting value for {SettingName}. Reason: {Reason}",
          setting.Name,
          normalizationResult.ErrorMessage);

        return HttpResult.Fail<TenantSettingsDto>(
          HttpResultErrorCode.ValidationFailed,
          normalizationResult.ErrorMessage ?? "Setting value is invalid.");
      }

      normalizationResults.Add((setting.Name, normalizationResult.Value));
    }

    foreach (var result in normalizationResults)
    {
      var existingSetting = tenant.TenantSettings.FirstOrDefault(x => x.Name == result.Name);
      if (string.IsNullOrWhiteSpace(result.Value))
      {
        if (existingSetting is not null)
        {
          tenant.TenantSettings.Remove(existingSetting);
        }

        continue;
      }

      if (existingSetting is not null)
      {
        existingSetting.Value = result.Value;
        continue;
      }

      tenant.TenantSettings.Add(new TenantSetting
      {
        Name = result.Name,
        Value = result.Value,
        TenantId = tenantId
      });
    }

    await _appDb.SaveChangesAsync(cancellationToken);
    return HttpResult.Ok(await GetAllSettings(tenantId, cancellationToken));
  }
}