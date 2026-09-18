using Asp.Versioning;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserPreferences;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Self-service user preferences for the calling user. The preferences are always owned by
/// the caller, so no operation addresses another principal. TenantId stays required on every
/// operation to keep the V1 convention uniform (server principals resolve the tenant check
/// but then fail the caller-has-no-user-id lookup, so the surface is user-only in practice).
/// Manager failures surface as ProblemDetails. A get of an unset name answers 204, and the POST
/// that leaves a preference behind answers 201. The internal twin answers 200 for that POST.
/// </summary>
[Route(HttpConstants.V1.UserPreferencesEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class UserPreferencesController : ControllerBase
{
  [HttpGet]
  [ProducesResponseType<UserPreferencesDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  public async Task<ActionResult<UserPreferencesDto>> GetAll(
    [FromServices] IUserPreferencesManager userPreferencesManager,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out _))
    {
      return Forbid();
    }

    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var preferences = await userPreferencesManager.GetAllPreferences(userId, cancellationToken);
    return Ok(ToV1Dto(preferences));
  }

  [HttpGet("{name}")]
  [ProducesResponseType<UserPreferenceResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  public async Task<ActionResult<UserPreferenceResponseDto>> GetPreference(
    [FromServices] AppDb appDb,
    [FromRoute] string name,
    [FromQuery] Guid tenantId)
  {
    if (!User.TryResolveTenantId(tenantId, out _))
    {
      return Forbid();
    }

    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var user = await appDb.Users
      .AsNoTracking()
      .Include(x => x.UserPreferences)
      .FirstOrDefaultAsync(x => x.Id == userId);

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

    return ToV1Dto(preference);
  }

  [HttpPost]
  [ProducesResponseType<UserPreferenceResponseDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  public async Task<ActionResult<UserPreferenceResponseDto>> SetPreference(
    [FromServices] IUserPreferencesManager userPreferencesManager,
    [FromQuery] Guid tenantId,
    [FromBody] UserPreferenceRequestDto preference,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var result = await userPreferencesManager.SetPreference(
      userId,
      new InternalDtos.UserPreferenceRequestDto(preference.Name, preference.Value),
      cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    var response = ToV1Dto(result.Value);

    // A blank value means the manager removed the preference, so there is no resource to point a
    // Location at. The manager signals that by leaving the id off the response.
    if (response.Id is null)
    {
      return Ok(response);
    }

    return CreatedAtAction(
      nameof(GetPreference),
      new { name = response.Name, tenantId = resolvedTenantId },
      response);
  }

  [HttpPut]
  [ProducesResponseType<UserPreferencesDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  public async Task<ActionResult<UserPreferencesDto>> SetPreferences(
    [FromServices] IUserPreferencesManager userPreferencesManager,
    [FromQuery] Guid tenantId,
    [FromBody] UserPreferencesDto preferences,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out _))
    {
      return Forbid();
    }

    if (!User.TryGetUserId(out var userId))
    {
      return Unauthorized();
    }

    var result = await userPreferencesManager.SetPreferences(
      userId,
      new InternalDtos.UserPreferencesDto(
        preferences.AutoQualityLowerThresholdMbps,
        preferences.AutoQualityMaximum,
        preferences.AutoQualityMinimum,
        preferences.AutoQualityUpperThresholdMbps,
        preferences.CaptureCursor,
        preferences.EncodingFormat,
        preferences.EnableDirectX,
        preferences.HideOfflineDevices,
        preferences.ShowOnlyUntaggedDevices,
        preferences.ShowOnlyUngroupedDevices,
        preferences.IsAutoQualityEnabled,
        preferences.IsMaxBandwidthEnabled,
        preferences.KeyboardInputMode,
        preferences.ManualQuality,
        preferences.MaxBandwidthMbps,
        preferences.NotifyUserOnSessionStart,
        preferences.OpenDeviceInNewTab,
        preferences.ThemeMode,
        preferences.UserDisplayName,
        preferences.ViewMode),
      cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(ToV1Dto(result.Value));
  }

  private static UserPreferenceResponseDto ToV1Dto(InternalDtos.UserPreferenceResponseDto preference)
  {
    return new UserPreferenceResponseDto(preference.Id, preference.Name, preference.Value);
  }

  private static UserPreferenceResponseDto ToV1Dto(Data.Entities.UserPreference preference)
  {
    return new UserPreferenceResponseDto(preference.Id, preference.Name, preference.Value);
  }

  private static UserPreferencesDto ToV1Dto(InternalDtos.UserPreferencesDto preferences)
  {
    return new UserPreferencesDto(
      preferences.AutoQualityLowerThresholdMbps,
      preferences.AutoQualityMaximum,
      preferences.AutoQualityMinimum,
      preferences.AutoQualityUpperThresholdMbps,
      preferences.CaptureCursor,
      preferences.EncodingFormat,
      preferences.EnableDirectX,
      preferences.HideOfflineDevices,
      preferences.ShowOnlyUntaggedDevices,
      preferences.ShowOnlyUngroupedDevices,
      preferences.IsAutoQualityEnabled,
      preferences.IsMaxBandwidthEnabled,
      preferences.KeyboardInputMode,
      preferences.ManualQuality,
      preferences.MaxBandwidthMbps,
      preferences.NotifyUserOnSessionStart,
      preferences.OpenDeviceInNewTab,
      preferences.ThemeMode,
      preferences.UserDisplayName,
      preferences.ViewMode);
  }
}
