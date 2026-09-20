using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Server.Services.DeviceFileSystem;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
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
  IOptionsMonitor<AppOptions> appOptions,
  ILogger<DeviceFileSystemController> logger) : ControllerBase
{
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;

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
  /// Packs the requested paths into one archive and streams it. The response is the archive itself,
  /// with the agent's own display name in the <c>Content-Disposition</c> header.
  /// </summary>
  [HttpPost("download-archive/{deviceId:guid}")]
  [DisableRequestTimeout]
  [BinaryResponse("application/octet-stream")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status408RequestTimeout, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413RequestEntityTooLarge, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, "application/problem+json")]
  public async Task<IActionResult> DownloadArchive(
    [FromRoute] Guid deviceId,
    [FromQuery] Guid tenantId,
    [FromBody] DownloadDeviceArchiveRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var archiveFileName = ArchiveFileNameHelper.NormalizeArchiveFileName(request.ArchiveFileName);
    if (archiveFileName is null)
    {
      return InvalidRequest("The archive file name is not a usable file name.");
    }

    if (request.TargetPaths is null || request.TargetPaths.Count == 0)
    {
      return InvalidRequest("At least one target path is required.");
    }

    var outcome = await _deviceFileSystem.StartArchiveDownload(
      User,
      deviceId,
      new InternalDtos.DownloadArchiveRequestDto(archiveFileName, request.TargetPaths),
      cancellationToken,
      resolvedTenantId);

    if (outcome is not { Succeeded: true, Value: { } session })
    {
      return MapFailure(outcome, "An error occurred during archive download.");
    }

    using (session)
    {
      return await StreamTransfer(
        session,
        "application/octet-stream",
        asAttachment: true,
        tooLargeDetail: "The archive is larger than the server's transfer limit.",
        cancellationToken);
    }
  }

  /// <summary>
  /// Streams one file from the device named by the route. The response is the file itself, with the
  /// agent's own display name in the <c>Content-Disposition</c> header.
  /// </summary>
  [HttpGet("download/{deviceId:guid}")]
  [DisableRequestTimeout]
  [BinaryResponse("application/octet-stream")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status408RequestTimeout, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413RequestEntityTooLarge, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, "application/problem+json")]
  public async Task<IActionResult> DownloadFile(
    [FromRoute] Guid deviceId,
    [FromQuery] Guid tenantId,
    [FromQuery] string filePath,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (string.IsNullOrWhiteSpace(filePath))
    {
      return InvalidRequest("A file path is required.");
    }

    var outcome = await _deviceFileSystem.StartFileDownload(
      User,
      deviceId,
      filePath,
      cancellationToken,
      resolvedTenantId);

    if (outcome is not { Succeeded: true, Value: { } session })
    {
      return MapFailure(outcome, "An error occurred during file download.");
    }

    using (session)
    {
      return await StreamTransfer(
        session,
        "application/octet-stream",
        asAttachment: true,
        tooLargeDetail: "The file is larger than the server's transfer limit.",
        cancellationToken);
    }
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
  /// Streams one file's contents as text, named by <paramref name="filePath"/>. The agent answers with
  /// any path it can read, so the caller needs the device's log-read permission rather than a
  /// directory listing first.
  /// </summary>
  [HttpGet("logs/{deviceId:guid}/contents")]
  [DisableRequestTimeout]
  [BinaryResponse("text/plain")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status408RequestTimeout, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, "application/problem+json")]
  public async Task<IActionResult> GetLogFileContents(
    [FromRoute] Guid deviceId,
    [FromQuery] Guid tenantId,
    [FromQuery] string filePath,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (string.IsNullOrWhiteSpace(filePath))
    {
      return InvalidRequest("A file path is required.");
    }

    var outcome = await _deviceFileSystem.StartLogFileContents(
      User,
      deviceId,
      filePath,
      cancellationToken,
      resolvedTenantId);

    if (outcome is not { Succeeded: true, Value: { } session })
    {
      return MapFailure(outcome, "An error occurred while streaming the log file.");
    }

    using (session)
    {
      // A log file's length is unknown until it ends, so there is no size to hold against the limit.
      return await StreamTransfer(
        session,
        "text/plain",
        asAttachment: false,
        tooLargeDetail: null,
        cancellationToken);
    }
  }

  /// <summary>
  /// Lists the log files the agent on <paramref name="deviceId"/> has on disk.
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

  // Note: [FromForm] parameters are intentionally omitted, so large files aren't buffered into memory
  // by model binding before the authorization and size checks run. [MultipartRequestBody] tells the
  // OpenAPI document the form fields instead.
  /// <summary>
  /// Streams one uploaded file to the device, which writes it into the requested directory.
  /// </summary>
  [HttpPost("upload/{deviceId:guid}")]
  [DisableRequestSizeLimit]
  [DisableRequestTimeout]
  [MultipartRequestBody]
  [ProducesResponseType<DeviceFileUploadResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status408RequestTimeout, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413RequestEntityTooLarge, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, "application/problem+json")]
  public async Task<IActionResult> UploadFile(
    [FromRoute] Guid deviceId,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    // Reading the form is what puts the body on disk, so the device has to have accepted the upload
    // first. Without this, any authenticated principal can spool a request body against a device the
    // caller cannot touch. UploadFile guards again, because it stays a complete operation.
    var authorization = await _deviceFileSystem.AuthorizeUpload(
      User,
      deviceId,
      cancellationToken,
      resolvedTenantId);

    if (!authorization.Succeeded)
    {
      return MapFailure(authorization, "An error occurred during file upload.");
    }

    if (!Request.HasFormContentType)
    {
      return InvalidRequest("Expected multipart/form-data content type.");
    }

    var maxFileSize = _appOptions.CurrentValue.MaxFileTransferSize;
    if (maxFileSize > 0 && Request.ContentLength > maxFileSize)
    {
      return TransferTooLarge("The upload is larger than the server's transfer limit.");
    }

    var form = await Request.ReadFormAsync(cancellationToken);
    var targetSaveDirectory = form["targetSaveDirectory"].ToString();
    var overwrite = bool.TryParse(form["overwrite"], out var overwriteValue) && overwriteValue;
    var file = form.Files.GetFile("file");

    if (file is null || file.Length == 0)
    {
      return InvalidRequest("A file is required.");
    }

    // The declared length above is a fast-fail before the body is spooled. A chunked upload declares
    // nothing, so the part's own length is what keeps the limit true for every transfer encoding.
    if (maxFileSize > 0 && file.Length > maxFileSize)
    {
      return TransferTooLarge("The upload is larger than the server's transfer limit.");
    }

    if (string.IsNullOrWhiteSpace(targetSaveDirectory))
    {
      return InvalidRequest("A target save directory is required.");
    }

    using var stream = file.OpenReadStream();

    var outcome = await _deviceFileSystem.UploadFile(
      User,
      deviceId,
      stream,
      file.FileName,
      file.Length,
      targetSaveDirectory,
      overwrite,
      cancellationToken,
      resolvedTenantId);

    if (!outcome.Succeeded)
    {
      return MapFailure(outcome, "An error occurred during file upload.");
    }

    return Ok(new DeviceFileUploadResponseDto("File uploaded successfully", file.FileName));
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
  /// The one place every operation translates an outcome's condition into a status.
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

  /// <summary>
  /// Writes a transfer to the response and drains what the agent sends, declaring no Content-Length.
  /// The size the agent gave is a snapshot taken before it read the file, so a file that grows or
  /// shrinks during the transfer makes that header false, and Kestrel answers one byte past a declared
  /// length by faulting the response. The first chunk starts the response, so a failure after that
  /// point cannot become a problem document and is left to propagate rather than being answered with a
  /// status the client can no longer see.
  /// </summary>
  private async Task<IActionResult> StreamTransfer(
    FileTransferSession session,
    string contentType,
    bool asAttachment,
    string? tooLargeDetail,
    CancellationToken cancellationToken)
  {
    var maxFileSize = _appOptions.CurrentValue.MaxFileTransferSize;
    if (tooLargeDetail is not null && maxFileSize > 0 && session.FileSize > maxFileSize)
    {
      return TransferTooLarge(tooLargeDetail);
    }

    Response.ContentType = contentType;

    var contentDisposition = new ContentDispositionHeaderValue(asAttachment ? "attachment" : "inline");
    contentDisposition.SetHttpFileName(session.FileName);
    Response.Headers[HeaderNames.ContentDisposition] = contentDisposition.ToString();

    try
    {
      await foreach (var chunk in session.ReadChunks())
      {
        if (chunk.Length > 0)
        {
          await Response.Body.WriteAsync(chunk, cancellationToken);
        }
      }

      return new EmptyResult();
    }
    catch (OperationCanceledException) when (!Response.HasStarted)
    {
      return Problem(
        detail: "The wait for the remote device was canceled.",
        statusCode: StatusCodes.Status408RequestTimeout,
        title: V1ProblemTitles.RequestTimedOut);
    }
    catch (Exception ex) when (!Response.HasStarted)
    {
      _logger.LogError(ex, "Error streaming {FileName} to the response.", session.FileName);

      return Problem(
        detail: "An error occurred while streaming from the device.",
        statusCode: StatusCodes.Status500InternalServerError,
        title: V1ProblemTitles.InternalServerError);
    }
  }

  private ObjectResult TransferTooLarge(string detail)
  {
    return Problem(
      detail: detail,
      statusCode: StatusCodes.Status413RequestEntityTooLarge,
      title: V1ProblemTitles.RequestEntityTooLarge);
  }
}
