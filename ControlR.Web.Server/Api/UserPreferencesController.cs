using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Extensions;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.UserPreferencesEndpoint)]
[ApiController]
[Authorize]
public class UserPreferencesController(AppDb appDb, IUserPreferencesManager userPreferencesManager) : ControllerBase
{
  private readonly AppDb _appDb = appDb;
  private readonly IUserPreferencesManager _userPreferencesManager = userPreferencesManager;

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
    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var result = await _userPreferencesManager.SetPreference(userId, preference);
    return result.ToActionResult();
  }
}
