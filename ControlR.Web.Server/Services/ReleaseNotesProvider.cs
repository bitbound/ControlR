using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Server.Primitives;

namespace ControlR.Web.Server.Services;

public interface IReleaseNotesProvider
{
  Task<HttpResult<string>> GetReleaseNotes(CancellationToken cancellationToken);
}

public class ReleaseNotesProvider(
  IWebHostEnvironment webHostEnvironment,
  ILogger<ReleaseNotesProvider> logger) : IReleaseNotesProvider
{
  private static readonly SemaphoreSlim _releaseNotesLock = new(1, 1);
  private static volatile string? _cachedReleaseNotes;

  public async Task<HttpResult<string>> GetReleaseNotes(CancellationToken cancellationToken)
  {
    try
    {
      if (_cachedReleaseNotes is not null)
      {
        return HttpResult.Ok(_cachedReleaseNotes);
      }

      using var heldLock = await _releaseNotesLock.AcquireLockAsync(cancellationToken);

      if (_cachedReleaseNotes is not null)
      {
        return HttpResult.Ok(_cachedReleaseNotes);
      }

      if (webHostEnvironment.IsDevelopment())
      {
        return await GetDevReleaseNotes(cancellationToken);
      }

      var fileInfo = webHostEnvironment.WebRootFileProvider.GetFileInfo("/downloads/release-notes.md");

      if (!fileInfo.Exists || string.IsNullOrWhiteSpace(fileInfo.PhysicalPath))
      {
        return HttpResult.Fail<string>(HttpResultErrorCode.NotFound, "Release notes file not found.");
      }

      await using var fs = new FileStream(fileInfo.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
      using var sr = new StreamReader(fs);
      var content = await sr.ReadToEndAsync(cancellationToken);

      _cachedReleaseNotes = content;
      return HttpResult.Ok(content);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error retrieving release notes.");
      return HttpResult.Fail<string>(ex, HttpResultErrorCode.InternalServerError, "Error retrieving release notes.");
    }
  }

  private async Task<HttpResult<string>> GetDevReleaseNotes(CancellationToken cancellationToken)
  {
    var solutionDirResult = IoHelper.GetSolutionDir();
    if (!solutionDirResult.IsSuccess)
    {
      return HttpResult.Fail<string>(HttpResultErrorCode.InternalServerError, solutionDirResult.Reason);
    }

    var releaseNotesPath = Path.Combine(solutionDirResult.Value, ".release-notes", "current.md");

    if (!File.Exists(releaseNotesPath))
    {
      return HttpResult.Fail<string>(HttpResultErrorCode.NotFound, "Release notes file not found.");
    }

    var content = await File.ReadAllTextAsync(releaseNotesPath, cancellationToken);
    return HttpResult.Ok(content);
  }
}