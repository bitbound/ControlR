using Asp.Versioning;
using ControlR.Web.Server.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using PrefsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserPreferences;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Self-service user preferences for the calling user. The preferences are always owned by
/// the caller, so no operation addresses another principal. TenantId stays required on every
/// operation to keep the V1 convention uniform (server principals resolve the tenant check
/// but then fail the caller-has-no-user-id lookup, so the surface is user-only in practice).
/// Manager failures surface as ProblemDetails. A get of an unset name answers 204.
/// </summary>
[Route(HttpConstants.V1.UserPreferencesEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class UserPreferencesController : ControllerBase
{
  [HttpGet]
  [ProducesResponseType<PrefsDtos.UserPreferencesDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<PrefsDtos.UserPreferencesDto>> GetAll(
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
  [ProducesResponseType<PrefsDtos.UserPreferenceResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<PrefsDtos.UserPreferenceResponseDto>> GetPreference(
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
  [ProducesResponseType<PrefsDtos.UserPreferenceResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<PrefsDtos.UserPreferenceResponseDto>> SetPreference(
    [FromServices] IUserPreferencesManager userPreferencesManager,
    [FromQuery] Guid tenantId,
    [FromBody] PrefsDtos.UserPreferenceRequestDto preference,
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

    var result = await userPreferencesManager.SetPreference(
      userId,
      new InternalDtos.UserPreferenceRequestDto(preference.Name, preference.Value),
      cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(ToV1Dto(result.Value));
  }

  [HttpPut]
  [ProducesResponseType<PrefsDtos.UserPreferencesDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<PrefsDtos.UserPreferencesDto>> SetPreferences(
    [FromServices] IUserPreferencesManager userPreferencesManager,
    [FromQuery] Guid tenantId,
    [FromBody] PrefsDtos.UserPreferencesDto preferences,
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

  private static PrefsDtos.UserPreferenceResponseDto ToV1Dto(InternalDtos.UserPreferenceResponseDto preference)
  {
    return new PrefsDtos.UserPreferenceResponseDto(preference.Id, preference.Name, preference.Value);
  }

  private static PrefsDtos.UserPreferenceResponseDto ToV1Dto(Data.Entities.UserPreference preference)
  {
    return new PrefsDtos.UserPreferenceResponseDto(preference.Id, preference.Name, preference.Value);
  }

  private static PrefsDtos.UserPreferencesDto ToV1Dto(InternalDtos.UserPreferencesDto preferences)
  {
    return new PrefsDtos.UserPreferencesDto(
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
