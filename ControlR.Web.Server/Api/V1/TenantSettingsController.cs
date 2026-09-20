using Asp.Versioning;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.TenantSettings;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Settings owned by a tenant, addressed by tenantId on every operation.
/// </summary>
[Route(HttpConstants.V1.TenantSettingsEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class TenantSettingsController : ControllerBase
{
  [HttpDelete("{settingName}")]
  [Authorize(Policy = PolicyNames.RequireTenantSettingsWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  public async Task<IActionResult> DeleteSetting(
    [FromServices] AppDb appDb,
    [FromRoute] string settingName,
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
    var setting = tenant.TenantSettings.FirstOrDefault(x => x.Name == settingName);

    if (setting is not null)
    {
      tenant.TenantSettings.Remove(setting);
      await appDb.SaveChangesAsync();
    }

    return NoContent();
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireTenantSettingsRead)]
  [ProducesResponseType<TenantSettingsDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  public async Task<ActionResult<TenantSettingsDto>> GetAll(
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

  [HttpGet("{settingName}")]
  [Authorize(Policy = PolicyNames.RequireTenantSettingsRead)]
  [ProducesResponseType<TenantSettingResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  public async Task<ActionResult<TenantSettingResponseDto>> GetSetting(
    [FromServices] AppDb appDb,
    [FromRoute] string settingName,
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
    var setting = tenant.TenantSettings.FirstOrDefault(x => x.Name == settingName);

    if (setting is null)
    {
      return NoContent();
    }

    return ToV1Dto(setting);
  }

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireTenantSettingsWrite)]
  [ProducesResponseType<TenantSettingResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType<TenantSettingResponseDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  public async Task<ActionResult<TenantSettingResponseDto>> SetSetting(
    [FromServices] ITenantSettingsManager tenantSettingsManager,
    [FromQuery] Guid tenantId,
    [FromBody] TenantSettingRequestDto setting)
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

    var response = ToV1Dto(result.Value);

    // A blank value means the manager removed the setting, so there is no resource to point a
    // Location at. The manager signals that by leaving the id off the response.
    if (response.Id is null)
    {
      return Ok(response);
    }

    return CreatedAtAction(
      nameof(GetSetting),
      new { settingName = response.Name, tenantId = resolvedTenantId },
      response);
  }

  [HttpPut]
  [Authorize(Policy = PolicyNames.RequireTenantSettingsWrite)]
  [ProducesResponseType<TenantSettingsDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  public async Task<ActionResult<TenantSettingsDto>> SetSettings(
    [FromServices] ITenantSettingsManager tenantSettingsManager,
    [FromQuery] Guid tenantId,
    [FromBody] TenantSettingsDto settings,
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

  private static TenantSettingResponseDto ToV1Dto(Data.Entities.TenantSetting setting)
  {
    return new TenantSettingResponseDto(setting.Id, setting.Name, setting.Value);
  }

  private static TenantSettingResponseDto ToV1Dto(InternalDtos.TenantSettingResponseDto setting)
  {
    return new TenantSettingResponseDto(setting.Id, setting.Name, setting.Value);
  }

  private static TenantSettingsDto ToV1Dto(InternalDtos.TenantSettingsDto settings)
  {
    return new TenantSettingsDto(
      settings.AppendInstanceId,
      settings.InstanceId,
      settings.NotifyUserOnSessionStart);
  }
}
