using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.UserStorageEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class UserStorageController(IUserStorageManager userStorageManager) : ControllerBase
{
  private readonly IUserStorageManager _userStorageManager = userStorageManager;

  [HttpDelete("{key}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> DeleteItem(string key, CancellationToken cancellationToken)
  {
    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var deleted = await _userStorageManager.Delete(key, userId, cancellationToken);
    return deleted ? NoContent() : NotFound();
  }

  [HttpGet("{key}")]
  [ProducesResponseType(typeof(InternalDtos.UserStorageResponseDto), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  public async Task<ActionResult<InternalDtos.UserStorageResponseDto>> GetItem(string key, CancellationToken cancellationToken)
  {
    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var value = await _userStorageManager.Get(key, userId, cancellationToken);
    if (value is null) return NoContent();
    return Ok(value);
  }

  [HttpPost]
  [ProducesResponseType(typeof(InternalDtos.UserStorageResponseDto), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<ActionResult<InternalDtos.UserStorageResponseDto>> SetItem([FromBody] InternalDtos.UserStorageRequestDto request, CancellationToken cancellationToken)
  {
    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var result = await _userStorageManager.Set(request.Key, request.Value, userId, cancellationToken);
    return Ok(result);
  }
}
