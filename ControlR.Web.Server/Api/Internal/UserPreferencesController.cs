using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.UserPreferencesEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class UserPreferencesController(AppDb appDb, IUserPreferencesManager userPreferencesManager) : ControllerBase
{
  private readonly AppDb _appDb = appDb;
  private readonly IUserPreferencesManager _userPreferencesManager = userPreferencesManager;

  [HttpGet]
  public async Task<ActionResult<UserPreferencesDto>> GetAll(CancellationToken cancellationToken)
  {
    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var preferences = await _userPreferencesManager.GetAllPreferences(userId, cancellationToken);
    return Ok(preferences);
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

  [HttpPut]
  public async Task<ActionResult<UserPreferencesDto>> SetPreferences(
    [FromBody] UserPreferencesDto preferences,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var result = await _userPreferencesManager.SetPreferences(userId, preferences, cancellationToken);
    return result.ToActionResult();
  }
}
