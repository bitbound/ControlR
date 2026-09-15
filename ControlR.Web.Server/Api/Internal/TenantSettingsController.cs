using ControlR.Web.Server.Extensions.Dtos.Internal;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.TenantSettingsEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class TenantSettingsController(AppDb appDb, ITenantSettingsManager tenantSettingsManager) : ControllerBase
{
  private readonly AppDb _appDb = appDb;
  private readonly ITenantSettingsManager _tenantSettingsManager = tenantSettingsManager;

  [HttpDelete("{name}")]
  [ApiDeprecated("/api/v1/tenant-settings/{name}?tenantId={tenantId}", Note = "Use DELETE /api/v1/tenant-settings/{name} with an explicit tenantId.")]
  [Authorize(Policy = PolicyNames.RequireTenantSettingsWrite)]
  public async Task<ActionResult> DeleteSetting(string name)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var tenant = await _appDb.Tenants
      .Include(x => x.TenantSettings)
      .FirstOrDefaultAsync(x => x.Id == tenantId);

    if (tenant is null)
    {
      return NotFound();
    }

    tenant.TenantSettings ??= [];
    var setting = tenant.TenantSettings.FirstOrDefault(x => x.Name == name);

    if (setting is not null)
    {
      tenant.TenantSettings.Remove(setting);
      await _appDb.SaveChangesAsync();
    }

    return NoContent();
  }

  [HttpGet]
  [ApiDeprecated("/api/v1/tenant-settings?tenantId={tenantId}", Note = "Use GET /api/v1/tenant-settings with an explicit tenantId.")]
  [Authorize(Policy = PolicyNames.RequireTenantSettingsRead)]
  public async Task<ActionResult<InternalDtos.TenantSettingsDto>> GetAll(CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var settings = await _tenantSettingsManager.GetAllSettings(tenantId, cancellationToken);
    return Ok(settings);
  }

  [HttpGet("{name}")]
  [ApiDeprecated("/api/v1/tenant-settings/{name}?tenantId={tenantId}", Note = "Use GET /api/v1/tenant-settings/{name} with an explicit tenantId.")]
  [Authorize(Policy = PolicyNames.RequireTenantSettingsRead)]
  public async Task<ActionResult<InternalDtos.TenantSettingResponseDto?>> GetSetting(string name)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var tenant = await _appDb.Tenants
      .AsNoTracking()
      .Include(x => x.TenantSettings)
      .FirstOrDefaultAsync(x => x.Id == tenantId);

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

    return setting.ToInternalResponseDto();
  }

  [HttpPost]
  [ApiDeprecated("/api/v1/tenant-settings?tenantId={tenantId}", Note = "Use POST /api/v1/tenant-settings with an explicit tenantId.")]
  [Authorize(Policy = PolicyNames.RequireTenantSettingsWrite)]
  public async Task<ActionResult<InternalDtos.TenantSettingResponseDto>> SetSetting([FromBody] InternalDtos.TenantSettingRequestDto setting)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var result = await _tenantSettingsManager.SetSetting(tenantId, setting);
    return result.ToActionResult();
  }

  [HttpPut]
  [ApiDeprecated("/api/v1/tenant-settings?tenantId={tenantId}", Note = "Use PUT /api/v1/tenant-settings with an explicit tenantId.")]
  [Authorize(Policy = PolicyNames.RequireTenantSettingsWrite)]
  public async Task<ActionResult<InternalDtos.TenantSettingsDto>> SetSettings(
    [FromBody] InternalDtos.TenantSettingsDto settings,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var result = await _tenantSettingsManager.SetSettings(tenantId, settings, cancellationToken);
    return result.ToActionResult();
  }
}
