using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Authz.Policies;
using ControlR.Web.Server.Extensions;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.TenantSettingsEndpoint + "/server/{tenantId:guid}")]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
public class ServerTenantSettingsController(AppDb appDb, ITenantSettingsManager tenantSettingsManager) : ControllerBase
{
  private readonly AppDb _appDb = appDb;
  private readonly ITenantSettingsManager _tenantSettingsManager = tenantSettingsManager;

  [HttpDelete("{name}")]
  public async Task<ActionResult> DeleteSetting(
    [FromRoute] Guid tenantId,
    [FromRoute] string name)
  {
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
  public async Task<ActionResult<TenantSettingsDto>> GetAll(
    [FromRoute] Guid tenantId,
    CancellationToken cancellationToken)
  {
    var settings = await _tenantSettingsManager.GetAllSettings(tenantId, cancellationToken);
    return Ok(settings);
  }

  [HttpGet("{name}")]
  public async Task<ActionResult<TenantSettingResponseDto?>> GetSetting(
    [FromRoute] Guid tenantId,
    [FromRoute] string name)
  {
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
  public async Task<ActionResult<TenantSettingResponseDto>> SetSetting(
    [FromRoute] Guid tenantId,
    [FromBody] TenantSettingRequestDto setting)
  {
    var result = await _tenantSettingsManager.SetSetting(tenantId, setting);
    return result.ToActionResult();
  }

  [HttpPut]
  public async Task<ActionResult<TenantSettingsDto>> SetSettings(
    [FromRoute] Guid tenantId,
    [FromBody] TenantSettingsDto settings,
    CancellationToken cancellationToken)
  {
    var result = await _tenantSettingsManager.SetSettings(tenantId, settings, cancellationToken);
    return result.ToActionResult();
  }
}