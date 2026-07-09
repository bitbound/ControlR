using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.TenantSettingsEndpoint)]
[ApiController]
[Authorize(Policy = RequireUserPrincipalPolicy.PolicyName)]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class TenantSettingsController(AppDb appDb, ITenantSettingsManager tenantSettingsManager) : ControllerBase
{
  private readonly AppDb _appDb = appDb;
  private readonly ITenantSettingsManager _tenantSettingsManager = tenantSettingsManager;

  [HttpDelete("{name}")]
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
  public async Task<ActionResult<TenantSettingsDto>> GetAll(CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var settings = await _tenantSettingsManager.GetAllSettings(tenantId, cancellationToken);
    return Ok(settings);
  }

  [HttpGet("{name}")]
  public async Task<ActionResult<TenantSettingResponseDto?>> GetSetting(string name)
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

    return setting.ToDto();
  }

  [HttpPost]
  public async Task<ActionResult<TenantSettingResponseDto>> SetSetting([FromBody] TenantSettingRequestDto setting)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var result = await _tenantSettingsManager.SetSetting(tenantId, setting);
    return result.ToActionResult();
  }

  [HttpPut]
  public async Task<ActionResult<TenantSettingsDto>> SetSettings(
    [FromBody] TenantSettingsDto settings,
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
