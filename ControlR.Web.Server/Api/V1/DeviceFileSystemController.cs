using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;
using ControlR.Web.Server.Services.DeviceFileSystem;
using Microsoft.AspNetCore.Mvc;
using ControlR.Web.Server.Constants;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Device file system operations for the versioned API. Each action asks a connected agent over the
/// hub and returns the V1 shapes.
/// </summary>
[Route(HttpConstants.V1.DeviceFileSystemEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class DeviceFileSystemController(
  IDeviceFileSystemService deviceFileSystem,
  ILogger<DeviceFileSystemController> logger) : ControllerBase
{
  private readonly IDeviceFileSystemService _deviceFileSystem = deviceFileSystem;

  private readonly ILogger<DeviceFileSystemController> _logger = logger;

  /// <summary>
  /// Creates a directory under <paramref name="deviceId"/>'s <c>ParentPath</c>. Answers 204 once the
  /// agent has accepted the request.
  /// </summary>
  [HttpPost("create-directory/{deviceId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status408RequestTimeout, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, "application/problem+json")]
  public async Task<IActionResult> CreateDirectory(
    [FromRoute] Guid deviceId,
    [FromQuery] Guid tenantId,
    [FromBody] CreateDeviceDirectoryRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (string.IsNullOrWhiteSpace(request.ParentPath) || string.IsNullOrWhiteSpace(request.DirectoryName))
    {
      return InvalidRequest("Parent path and directory name are required.");
    }

    var outcome = await _deviceFileSystem.CreateDirectory(
      User,
      deviceId,
      new InternalDtos.CreateDirectoryRequestDto(deviceId, request.ParentPath, request.DirectoryName),
      cancellationToken,
      resolvedTenantId);

    if (!outcome.Succeeded)
    {
      return MapFailure(outcome, "An error occurred during directory creation.");
    }

    return NoContent();
  }

  /// <summary>
  /// Deletes one path on <paramref name="deviceId"/>. Answers 200 with the path that was deleted.
  /// </summary>
  [HttpDelete("delete-path/{deviceId:guid}")]
  [ProducesResponseType<DevicePathDeletionResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status408RequestTimeout, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, "application/problem+json")]
  public async Task<IActionResult> DeletePath(
    [FromRoute] Guid deviceId,
    [FromQuery] Guid tenantId,
    [FromBody] DeleteDevicePathRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (string.IsNullOrWhiteSpace(request.FilePath))
    {
      return InvalidRequest("A path is required.");
    }

    // There is no directory flag on either contract. The agent stats the path and deletes a directory
    // tree or a file accordingly.
    var outcome = await _deviceFileSystem.DeletePath(
      User,
      deviceId,
      new InternalDtos.DeletePathRequestDto(deviceId, request.FilePath),
      cancellationToken,
      resolvedTenantId);

    if (!outcome.Succeeded)
    {
      return MapFailure(outcome, "An error occurred during path deletion.");
    }

    return Ok(new DevicePathDeletionResponseDto("Path deletion completed", request.FilePath));
  }

  /// <summary>
  /// Lists the entries of one directory on the device named in the body.
  /// </summary>
  [HttpPost("contents")]
  [ProducesResponseType<DeviceDirectoryContentsResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status408RequestTimeout, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, "application/problem+json")]
  public async Task<IActionResult> GetDirectoryContents(
    [FromQuery] Guid tenantId,
    [FromBody] DeviceDirectoryContentsRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var outcome = await _deviceFileSystem.GetDirectoryContents(
      User,
      new InternalDtos.GetDirectoryContentsRequestDto(request.DeviceId, request.DirectoryPath),
      cancellationToken,
      resolvedTenantId);

    if (outcome is not { Succeeded: true, Value: { } contents })
    {
      return MapFailure(outcome, "An error occurred while retrieving directory contents.");
    }

    return Ok(ToV1Dto(contents));
  }

  /// <summary>
  /// Lists the log files the agent on <paramref name="deviceId"/> has on disk. The log-file *contents*
  /// stay internal, because a raw text stream is not expressible in the JSON client.
  /// </summary>
  [HttpGet("logs/{deviceId:guid}")]
  [ProducesResponseType<DeviceLogFileListResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status408RequestTimeout, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, "application/problem+json")]
  public async Task<IActionResult> GetLogFiles(
    [FromRoute] Guid deviceId,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var outcome = await _deviceFileSystem.GetLogFiles(
      User,
      deviceId,
      cancellationToken,
      resolvedTenantId);

    if (outcome is not { Succeeded: true, Value: { } logFiles })
    {
      return MapFailure(outcome, "An error occurred while retrieving log files.");
    }

    return Ok(ToV1Dto(logFiles));
  }

  /// <summary>
  /// Asks the device named in the body how it would split a path. A device that does not exist is a
  /// 404 here as on every other action, unlike the deprecated endpoint that answered 400.
  /// </summary>
  [HttpPost("path-segments")]
  [ProducesResponseType<DevicePathSegmentsResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status408RequestTimeout, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, "application/problem+json")]
  public async Task<IActionResult> GetPathSegments(
    [FromQuery] Guid tenantId,
    [FromBody] DevicePathSegmentsRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var outcome = await _deviceFileSystem.GetPathSegments(
      User,
      new InternalDtos.GetPathSegmentsRequestDto(request.DeviceId, request.TargetPath),
      cancellationToken,
      resolvedTenantId);

    if (outcome is not { Succeeded: true, Value: { } segments })
    {
      return MapFailure(outcome, "An error occurred while getting path segments.");
    }

    return Ok(ToV1Dto(segments));
  }

  /// <summary>
  /// Lists the file system entries at the roots of the device named in the body.
  /// </summary>
  [HttpPost("root-drives")]
  [ProducesResponseType<DeviceRootDrivesResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status408RequestTimeout, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, "application/problem+json")]
  public async Task<IActionResult> GetRootDrives(
    [FromQuery] Guid tenantId,
    [FromBody] DeviceRootDrivesRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var outcome = await _deviceFileSystem.GetRootDrives(
      User,
      new InternalDtos.GetRootDrivesRequestDto(request.DeviceId),
      cancellationToken,
      resolvedTenantId);

    if (outcome is not { Succeeded: true, Value: { } drives })
    {
      return MapFailure(outcome, "An error occurred while retrieving root drives.");
    }

    return Ok(ToV1Dto(drives));
  }

  /// <summary>
  /// Lists the immediate subdirectories of one directory on the device named in the body.
  /// </summary>
  [HttpPost("subdirectories")]
  [ProducesResponseType<DeviceSubdirectoriesResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status408RequestTimeout, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, "application/problem+json")]
  public async Task<IActionResult> GetSubdirectories(
    [FromQuery] Guid tenantId,
    [FromBody] DeviceSubdirectoriesRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var outcome = await _deviceFileSystem.GetSubdirectories(
      User,
      new InternalDtos.GetSubdirectoriesRequestDto(request.DeviceId, request.DirectoryPath),
      cancellationToken,
      resolvedTenantId);

    if (outcome is not { Succeeded: true, Value: { } subdirectories })
    {
      return MapFailure(outcome, "An error occurred while retrieving subdirectories.");
    }

    return Ok(ToV1Dto(subdirectories));
  }

  /// <summary>
  /// Asks the device whether a directory and a file name combine into a usable path. An answer that
  /// the path is invalid is a 200, because the question was answered.
  /// </summary>
  [HttpPost("validate-path/{deviceId:guid}")]
  [ProducesResponseType<DeviceFilePathValidationResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status408RequestTimeout, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, "application/problem+json")]
  public async Task<IActionResult> ValidateFilePath(
    [FromRoute] Guid deviceId,
    [FromQuery] Guid tenantId,
    [FromBody] ValidateDeviceFilePathRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (string.IsNullOrWhiteSpace(request.DirectoryPath) || string.IsNullOrWhiteSpace(request.FileName))
    {
      return InvalidRequest("Directory path and file name are required.");
    }

    var outcome = await _deviceFileSystem.ValidateFilePath(
      User,
      deviceId,
      new InternalDtos.ValidateFilePathRequestDto(deviceId, request.DirectoryPath, request.FileName),
      cancellationToken,
      resolvedTenantId);

    if (outcome is not { Succeeded: true, Value: { } validation })
    {
      return MapFailure(outcome, "An error occurred while validating the file path.");
    }

    return Ok(ToV1Dto(validation));
  }

  // The collections below are agent-supplied and null when an older agent omits them. These mappers run
  // after the service's try/catch, so an unguarded dereference surfaces as an unmapped 500.
  private static DeviceDirectoryContentsResponseDto ToV1Dto(
    InternalDtos.GetDirectoryContentsResponseDto source)
  {
    return new DeviceDirectoryContentsResponseDto(
      [.. (source.Items ?? []).Select(ToV1Dto)],
      source.DirectoryExists);
  }

  private static DeviceFilePathValidationResponseDto ToV1Dto(
    InternalDtos.ValidateFilePathResponseDto source)
  {
    return new DeviceFilePathValidationResponseDto(source.IsValid, source.ErrorMessage);
  }

  private static DeviceFileSystemEntryDto ToV1Dto(InternalDtos.FileSystemEntryDto source)
  {
    return new DeviceFileSystemEntryDto(
      source.Name,
      source.FullPath,
      source.IsDirectory,
      source.Size,
      source.LastModified,
      source.IsHidden,
      source.CanRead,
      source.CanWrite,
      source.HasSubfolders);
  }

  private static DeviceLogFileEntryDto ToV1Dto(InternalDtos.LogFileEntryDto source)
  {
    return new DeviceLogFileEntryDto(
      source.FileName,
      source.FullPath,
      source.Size,
      source.LastModified);
  }

  private static DeviceLogFileGroupDto ToV1Dto(InternalDtos.LogFileGroupDto source)
  {
    return new DeviceLogFileGroupDto(source.GroupName, [.. (source.LogFiles ?? []).Select(ToV1Dto)]);
  }

  private static DeviceLogFileListResponseDto ToV1Dto(InternalDtos.GetLogFilesResponseDto source)
  {
    return new DeviceLogFileListResponseDto([.. (source.LogFileGroups ?? []).Select(ToV1Dto)]);
  }

  private static DevicePathSegmentsResponseDto ToV1Dto(InternalDtos.PathSegmentsResponseDto source)
  {
    return new DevicePathSegmentsResponseDto(
      source.ErrorMessage,
      source.PathExists,
      [.. source.PathSegments ?? []],
      source.PathSeparator,
      source.Success);
  }

  private static DeviceRootDrivesResponseDto ToV1Dto(InternalDtos.GetRootDrivesResponseDto source)
  {
    return new DeviceRootDrivesResponseDto([.. (source.Drives ?? []).Select(ToV1Dto)]);
  }

  private static DeviceSubdirectoriesResponseDto ToV1Dto(
    InternalDtos.GetSubdirectoriesResponseDto source)
  {
    return new DeviceSubdirectoriesResponseDto([.. (source.Subdirectories ?? []).Select(ToV1Dto)]);
  }

  private ObjectResult InvalidRequest(string detail)
  {
    return Problem(
      detail: detail,
      statusCode: StatusCodes.Status400BadRequest,
      title: V1ProblemTitles.InvalidRequest);
  }

  /// <summary>
  /// The one place the eight operations translate an outcome's condition into a status.
  /// </summary>
  private IActionResult MapFailure(
    FileSystemOutcome outcome,
    string unexpectedFailureDetail)
  {
    if (outcome.Failure is FileSystemFailure.None)
    {
      // Reported success but carried no payload, which none of the payload-carrying operations do.
      // Reaching here is a bug in this server rather than a device fault, so the detail says
      // unexpected response and does not claim the device could not be contacted.
      _logger.LogError(
        "A device file system operation reported success without a payload ({Outcome}).",
        outcome);

      return Problem(
        detail: "The remote device returned an unexpected response.",
        statusCode: StatusCodes.Status500InternalServerError,
        title: V1ProblemTitles.InternalServerError);
    }

    return outcome.Failure switch
    {
      FileSystemFailure.DeviceNotFound => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: V1ProblemTitles.NotFound),
      FileSystemFailure.Forbidden => Forbid(),
      FileSystemFailure.DeviceOffline => Problem(
        detail: "Device is currently offline.",
        statusCode: StatusCodes.Status409Conflict,
        title: V1ProblemTitles.Conflict),
      FileSystemFailure.RemoteFailure => Problem(
        detail: outcome.Reason,
        statusCode: StatusCodes.Status409Conflict,
        title: V1ProblemTitles.Conflict),
      FileSystemFailure.NoResponse => Problem(
        detail: "The device did not return a result.",
        statusCode: StatusCodes.Status502BadGateway,
        title: V1ProblemTitles.BadGateway),
      FileSystemFailure.Cancelled => Problem(
        detail: "The wait for the remote device was canceled.",
        statusCode: StatusCodes.Status408RequestTimeout,
        title: V1ProblemTitles.RequestTimedOut),
      FileSystemFailure.Unexpected => Problem(
        detail: unexpectedFailureDetail,
        statusCode: StatusCodes.Status500InternalServerError,
        title: V1ProblemTitles.InternalServerError),
      _ => throw new ArgumentOutOfRangeException(
        nameof(outcome),
        outcome.Failure,
        "Unrecognized device file system failure condition."),
    };
  }
}
