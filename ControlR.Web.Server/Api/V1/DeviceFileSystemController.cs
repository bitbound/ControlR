using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;
using ControlR.Web.Server.Services.DeviceFileSystem;
using Microsoft.AspNetCore.Mvc;

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
  private const string DeviceOfflineMessage = "Device is not currently online.";

  private readonly IDeviceFileSystemService _deviceFileSystem = deviceFileSystem;

  private readonly ILogger<DeviceFileSystemController> _logger = logger;

  /// <summary>
  /// Creates a directory under <paramref name="deviceId"/>'s <c>ParentPath</c>. Answers 204 once the
  /// agent has accepted the request.
  /// </summary>
  [HttpPost("create-directory/{deviceId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
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
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
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
      return InvalidRequest("File path is required.");
    }

    // The agent's delete carries only the path, so the internal request's directory flag has no
    // counterpart in this contract and nothing downstream reads it.
    var outcome = await _deviceFileSystem.DeletePath(
      User,
      deviceId,
      new InternalDtos.FileDeleteRequestDto(deviceId, request.FilePath, IsDirectory: false),
      cancellationToken,
      resolvedTenantId);

    if (!outcome.Succeeded)
    {
      return MapFailure(outcome, "An error occurred during file deletion.");
    }

    return Ok(new DevicePathDeletionResponseDto("File deletion completed", request.FilePath));
  }

  /// <summary>
  /// Lists the entries of one directory on the device named in the body.
  /// </summary>
  [HttpPost("contents")]
  [ProducesResponseType<DeviceDirectoryContentsResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status408RequestTimeout)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
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
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
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
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
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
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
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
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status408RequestTimeout)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
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
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
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
      title: "Invalid request.");
  }

  /// <summary>
  /// The one place the eight operations translate an outcome's condition into a status.
  /// </summary>
  private IActionResult MapFailure<TValue>(
    FileSystemOutcome<TValue> outcome,
    string unexpectedFailureDetail)
  {
    if (outcome.Failure is FileSystemFailure.None)
    {
      // Reported success but carried no payload, which no operation produces today. Reaching here is a
      // bug in this server rather than a device fault, so the title says unexpected response and does
      // not claim the device could not be contacted.
      _logger.LogError(
        "A device file system operation reported success without a payload ({Outcome}).",
        outcome);

      return Problem(
        detail: unexpectedFailureDetail,
        statusCode: StatusCodes.Status500InternalServerError,
        title: "The remote device returned an unexpected response.");
    }

    return outcome.Failure switch
    {
      FileSystemFailure.DeviceNotFound => NotFound(),
      FileSystemFailure.Forbidden => Forbid(),
      FileSystemFailure.DeviceOffline => Problem(
        detail: DeviceOfflineMessage,
        statusCode: StatusCodes.Status409Conflict,
        title: "The device is not currently online."),
      FileSystemFailure.HubRejected when outcome.Reason is { Length: > 0 } reason => Problem(
        detail: reason,
        statusCode: StatusCodes.Status409Conflict,
        title: "The remote device could not complete the operation."),
      FileSystemFailure.HubRejected => Problem(
        detail: "The device did not return a result.",
        statusCode: StatusCodes.Status502BadGateway,
        title: "No response from the remote device."),
      FileSystemFailure.Cancelled => StatusCode(StatusCodes.Status408RequestTimeout),
      FileSystemFailure.Unexpected => Problem(
        detail: unexpectedFailureDetail,
        statusCode: StatusCodes.Status500InternalServerError,
        title: "Error contacting the remote device."),
      _ => throw new ArgumentOutOfRangeException(
        nameof(outcome),
        outcome.Failure,
        "Unrecognized device file system failure condition."),
    };
  }
}
