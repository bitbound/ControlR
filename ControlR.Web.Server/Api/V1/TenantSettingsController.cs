using Asp.Versioning;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using SettingsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.TenantSettings;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Tenant settings resource. Same operations as the internal surface with the V1 conventions:
/// required tenantId (so service accounts can address a tenant explicitly), ProblemDetails on
/// validation failures, and 204 for delete and for a get of an unset name.
/// </summary>
[Route(HttpConstants.V1.TenantSettingsEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class TenantSettingsController : ControllerBase
{
  [HttpDelete("{name}")]
  [Authorize(Policy = PolicyNames.RequireTenantSettingsWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> DeleteSetting(
    [FromServices] AppDb appDb,
    [FromRoute] string name,
    [FromQuery] Guid tenantId)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var tenant = await appDb.Tenants
      .Include(x => x.TenantSettings)
      .FirstOrDefaultAsync(x => x.Id == resolvedTenantId);

    if (tenant is null)
    {
      return NotFound();
    }

    tenant.TenantSettings ??= [];
    var setting = tenant.TenantSettings.FirstOrDefault(x => x.Name == name);

    if (setting is not null)
    {
      tenant.TenantSettings.Remove(setting);
      await appDb.SaveChangesAsync();
    }

    return NoContent();
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireTenantSettingsRead)]
  [ProducesResponseType<SettingsDtos.TenantSettingsDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<SettingsDtos.TenantSettingsDto>> GetAll(
    [FromServices] ITenantSettingsManager tenantSettingsManager,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var settings = await tenantSettingsManager.GetAllSettings(resolvedTenantId, cancellationToken);
    return Ok(ToV1Dto(settings));
  }

  [HttpGet("{name}")]
  [Authorize(Policy = PolicyNames.RequireTenantSettingsRead)]
  [ProducesResponseType<SettingsDtos.TenantSettingResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<SettingsDtos.TenantSettingResponseDto>> GetSetting(
    [FromServices] AppDb appDb,
    [FromRoute] string name,
    [FromQuery] Guid tenantId)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var tenant = await appDb.Tenants
      .AsNoTracking()
      .Include(x => x.TenantSettings)
      .FirstOrDefaultAsync(x => x.Id == resolvedTenantId);

    if (tenant is null)
    {
      return NotFound();
    }

    tenant.TenantSettings ??= [];
    var setting = tenant.TenantSettings.FirstOrDefault(x => x.Name == name);

    if (setting is null)
    {
      return NoContent();
    }

    return ToV1Dto(setting);
  }

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireTenantSettingsWrite)]
  [ProducesResponseType<SettingsDtos.TenantSettingResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<SettingsDtos.TenantSettingResponseDto>> SetSetting(
    [FromServices] ITenantSettingsManager tenantSettingsManager,
    [FromQuery] Guid tenantId,
    [FromBody] SettingsDtos.TenantSettingRequestDto setting)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var result = await tenantSettingsManager.SetSetting(
      resolvedTenantId,
      new InternalDtos.TenantSettingRequestDto(setting.Name, setting.Value));

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(ToV1Dto(result.Value));
  }

  [HttpPut]
  [Authorize(Policy = PolicyNames.RequireTenantSettingsWrite)]
  [ProducesResponseType<SettingsDtos.TenantSettingsDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<SettingsDtos.TenantSettingsDto>> SetSettings(
    [FromServices] ITenantSettingsManager tenantSettingsManager,
    [FromQuery] Guid tenantId,
    [FromBody] SettingsDtos.TenantSettingsDto settings,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var result = await tenantSettingsManager.SetSettings(
      resolvedTenantId,
      new InternalDtos.TenantSettingsDto(
        settings.AppendInstanceId,
        settings.InstanceId,
        settings.NotifyUserOnSessionStart),
      cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(ToV1Dto(result.Value));
  }

  private static SettingsDtos.TenantSettingResponseDto ToV1Dto(Data.Entities.TenantSetting setting)
  {
    return new SettingsDtos.TenantSettingResponseDto(setting.Id, setting.Name, setting.Value);
  }

  private static SettingsDtos.TenantSettingResponseDto ToV1Dto(InternalDtos.TenantSettingResponseDto setting)
  {
    return new SettingsDtos.TenantSettingResponseDto(setting.Id, setting.Name, setting.Value);
  }

  private static SettingsDtos.TenantSettingsDto ToV1Dto(InternalDtos.TenantSettingsDto settings)
  {
    return new SettingsDtos.TenantSettingsDto(
      settings.AppendInstanceId,
      settings.InstanceId,
      settings.NotifyUserOnSessionStart);
  }
}
