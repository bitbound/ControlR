using ControlR.Libraries.Shared.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.UserPreferencesEndpoint)]
[ApiController]
[Authorize]
public class UserPreferencesController(AppDb appDb) : ControllerBase
{
  private readonly AppDb _appDb = appDb;

  [HttpGet]
  public async IAsyncEnumerable<UserPreferenceResponseDto> GetAll()
  {
    if (User.Identity is null)
    {
      yield break;
    }

    var user = await _appDb.Users
      .AsNoTracking()
      .Include(x => x.UserPreferences)
      .FirstOrDefaultAsync(x => x.UserName == User.Identity.Name);

    if (user?.UserPreferences is null)
    {
      yield break;
    }

    foreach (var preference in user.UserPreferences)
    {
      yield return preference.ToDto();
    }
  }

  [HttpGet("{name}")]
  public async Task<ActionResult<UserPreferenceResponseDto?>> GetPreference(string name)
  {
    if (User.Identity is null)
    {
      return Unauthorized();
    }

    var user = await _appDb.Users
      .AsNoTracking()
      .Include(x => x.UserPreferences)
      .FirstOrDefaultAsync(x => x.UserName == User.Identity.Name);

    if (user is null)
    {
      return NotFound();
    }

    user.UserPreferences ??= [];
    var preference = user.UserPreferences.FirstOrDefault(x => x.Name == name);

    if (preference is null)
    {
      return NoContent();
    }

    return preference.ToDto();
  }

  [HttpPost]
  public async Task<ActionResult<UserPreferenceResponseDto>> SetPreference([FromBody] UserPreferenceRequestDto preference)
  {
    if (User.Identity is null)
    {
      return Unauthorized();
    }

    var user = await _appDb.Users
      .Include(x => x.UserPreferences)
      .FirstOrDefaultAsync(x => x.UserName == User.Identity.Name);

    if (user is null)
    {
      return NotFound();
    }

    var entity = new UserPreference()
    {
      Name = preference.Name,
      Value = preference.Value.Trim(),
      UserId = user.Id,
      TenantId = user.TenantId
    };

    user.UserPreferences ??= [];

    var index = user.UserPreferences.FindIndex(x => x.Name == preference.Name);

    if (index >= 0)
    {
      user.UserPreferences[index] = entity;
    }
    else
    {
      user.UserPreferences.Add(entity);
    }

    await _appDb.SaveChangesAsync();

    return entity.ToDto();
  }
}
