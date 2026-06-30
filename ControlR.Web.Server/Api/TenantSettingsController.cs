using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Extensions;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.TenantSettingsEndpoint)]
[ApiController]
[Authorize]
public class TenantSettingsController(AppDb appDb, ITenantSettingsManager tenantSettingsManager) : ControllerBase
{
  private readonly AppDb _appDb = appDb;
  private readonly ITenantSettingsManager _tenantSettingsManager = tenantSettingsManager;

  [HttpDelete("{name}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult> DeleteSetting(string name)
  {
    Guid? tenantId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid))
        return Unauthorized();
      tenantId = tid;
    }

    var tenant = await _appDb.Tenants
      .Include(x => x.TenantSettings)
      .FirstOrDefaultAsync(x => x.Id == tenantId!.Value);

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
  [Authorize]
  public async Task<ActionResult<TenantSettingsDto>> GetAll(CancellationToken cancellationToken)
  {
    Guid? tenantId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid))
        return Unauthorized();
      tenantId = tid;
    }

    var settings = await _tenantSettingsManager.GetAllSettings(tenantId!.Value, cancellationToken);
    return Ok(settings);
  }

  [HttpGet("{name}")]
  [Authorize]
  public async Task<ActionResult<TenantSettingResponseDto?>> GetSetting(string name)
  {
    Guid? tenantId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid))
        return Unauthorized();
      tenantId = tid;
    }

    var tenant = await _appDb.Tenants
      .AsNoTracking()
      .Include(x => x.TenantSettings)
      .FirstOrDefaultAsync(x => x.Id == tenantId!.Value);

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
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<TenantSettingResponseDto>> SetSetting([FromBody] TenantSettingRequestDto setting)
  {
    Guid? tenantId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid))
        return Unauthorized();
      tenantId = tid;
    }

    var result = await _tenantSettingsManager.SetSetting(tenantId!.Value, setting);
    return result.ToActionResult();
  }

  [HttpPut]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<TenantSettingsDto>> SetSettings(
    [FromBody] TenantSettingsDto settings,
    CancellationToken cancellationToken)
  {
    Guid? tenantId = null;
    if (!User.IsServerPrincipal())
    {
      if (!User.TryGetTenantId(out var tid))
        return Unauthorized();
      tenantId = tid;
    }

    var result = await _tenantSettingsManager.SetSettings(tenantId!.Value, settings, cancellationToken);
    return result.ToActionResult();
  }
}
