using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Authz.Policies;
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
  [Authorize(Policy = CombinedAuthorizationPolicies.RequireServerOrTenantAdminPolicy)]
  public async Task<ActionResult> DeleteSetting(string name, [FromQuery] Guid? tenantId)
  {
    Guid effectiveTenantId;

    if (User.IsServerPrincipal())
    {
      if (!tenantId.HasValue || tenantId == Guid.Empty)
      {
        return BadRequest("TenantId is required.");
      }

      effectiveTenantId = tenantId.Value;
    }
    else
    {
      if (!User.TryGetTenantId(out var tid))
      {
        return Unauthorized();
      }

      if (tenantId.HasValue && tenantId.Value != tid)
      {
        return Forbid();
      }

      effectiveTenantId = tid;
    }

    var tenant = await _appDb.Tenants
      .Include(x => x.TenantSettings)
      .FirstOrDefaultAsync(x => x.Id == effectiveTenantId);

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
  public async Task<ActionResult<TenantSettingsDto>> GetAll([FromQuery] Guid? tenantId, CancellationToken cancellationToken)
  {
    Guid effectiveTenantId;

    if (User.IsServerPrincipal())
    {
      if (!tenantId.HasValue || tenantId == Guid.Empty)
      {
        return BadRequest("TenantId is required.");
      }

      effectiveTenantId = tenantId.Value;
    }
    else
    {
      if (!User.TryGetTenantId(out var tid))
      {
        return Unauthorized();
      }

      if (tenantId.HasValue && tenantId.Value != tid)
      {
        return Forbid();
      }

      effectiveTenantId = tid;
    }

    var settings = await _tenantSettingsManager.GetAllSettings(effectiveTenantId, cancellationToken);
    return Ok(settings);
  }

  [HttpGet("{name}")]
  [Authorize]
  public async Task<ActionResult<TenantSettingResponseDto?>> GetSetting(string name, [FromQuery] Guid? tenantId)
  {
    Guid effectiveTenantId;

    if (User.IsServerPrincipal())
    {
      if (!tenantId.HasValue || tenantId == Guid.Empty)
      {
        return BadRequest("TenantId is required.");
      }

      effectiveTenantId = tenantId.Value;
    }
    else
    {
      if (!User.TryGetTenantId(out var tid))
      {
        return Unauthorized();
      }

      if (tenantId.HasValue && tenantId.Value != tid)
      {
        return Forbid();
      }

      effectiveTenantId = tid;
    }

    var tenant = await _appDb.Tenants
      .AsNoTracking()
      .Include(x => x.TenantSettings)
      .FirstOrDefaultAsync(x => x.Id == effectiveTenantId);

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
  [Authorize(Policy = CombinedAuthorizationPolicies.RequireServerOrTenantAdminPolicy)]
  public async Task<ActionResult<TenantSettingResponseDto>> SetSetting([FromBody] TenantSettingRequestDto setting)
  {
    Guid effectiveTenantId;

    if (User.IsServerPrincipal())
    {
      if (!setting.TenantId.HasValue || setting.TenantId == Guid.Empty)
      {
        return BadRequest("TenantId is required.");
      }

      effectiveTenantId = setting.TenantId.Value;
    }
    else
    {
      if (!User.TryGetTenantId(out var tid))
      {
        return Unauthorized();
      }

      if (setting.TenantId.HasValue && setting.TenantId.Value != tid)
      {
        return Forbid();
      }

      effectiveTenantId = tid;
    }

    var result = await _tenantSettingsManager.SetSetting(effectiveTenantId, setting);
    return result.ToActionResult();
  }

  [HttpPut]
  [Authorize(Policy = CombinedAuthorizationPolicies.RequireServerOrTenantAdminPolicy)]
  public async Task<ActionResult<TenantSettingsDto>> SetSettings(
    [FromBody] TenantSettingsDto settings,
    CancellationToken cancellationToken)
  {
    Guid effectiveTenantId;

    if (User.IsServerPrincipal())
    {
      if (!settings.TenantId.HasValue || settings.TenantId == Guid.Empty)
      {
        return BadRequest("TenantId is required.");
      }

      effectiveTenantId = settings.TenantId.Value;
    }
    else
    {
      if (!User.TryGetTenantId(out var tid))
      {
        return Unauthorized();
      }

      if (settings.TenantId.HasValue && settings.TenantId.Value != tid)
      {
        return Forbid();
      }

      effectiveTenantId = tid;
    }

    var result = await _tenantSettingsManager.SetSettings(effectiveTenantId, settings, cancellationToken);
    return result.ToActionResult();
  }
}
