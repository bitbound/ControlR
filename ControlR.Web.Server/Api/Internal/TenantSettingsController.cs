using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.TenantSettingsEndpoint)]
[Route(HttpConstants.Legacy.TenantSettingsEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class TenantSettingsController(AppDb appDb, ITenantSettingsManager tenantSettingsManager) : ControllerBase
{
  private readonly AppDb _appDb = appDb;
  private readonly ITenantSettingsManager _tenantSettingsManager = tenantSettingsManager;

  [HttpDelete("{name}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
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
  [Authorize(Roles = RoleNames.TenantAdministrator)]
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
  [Authorize(Roles = RoleNames.TenantAdministrator)]
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
