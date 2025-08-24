using ControlR.Libraries.Shared.Constants;
using ControlR.Web.Client.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.TenantSettingsEndpoint)]
[ApiController]
[Authorize]
public class TenantSettingsController(AppDb appDb) : ControllerBase
{
  private readonly AppDb _appDb = appDb;

  [HttpGet]
  [Authorize]
  public async IAsyncEnumerable<TenantSettingResponseDto> GetAll()
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      yield break;
    }

    var tenant = await _appDb.Tenants
      .AsNoTracking()
      .Include(x => x.TenantSettings)
      .FirstOrDefaultAsync(x => x.Id == tenantId);

    if (tenant?.TenantSettings is null)
    {
      yield break;
    }

    foreach (var setting in tenant.TenantSettings)
    {
      yield return setting.ToDto();
    }
  }

  [HttpGet("{name}")]
  [Authorize]
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
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<TenantSettingResponseDto>> SetSetting([FromBody] TenantSettingRequestDto setting)
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

    var entity = new TenantSetting()
    {
      Name = setting.Name,
      Value = setting.Value,
      TenantId = tenantId
    };

    tenant.TenantSettings ??= [];

    var index = tenant.TenantSettings.FindIndex(x => x.Name == setting.Name);

    if (index >= 0)
    {
      tenant.TenantSettings[index] = entity;
    }
    else
    {
      tenant.TenantSettings.Add(entity);
    }

    await _appDb.SaveChangesAsync();
    return entity.ToDto();
  }

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
}
