using ControlR.Libraries.Shared.IO;

namespace ControlR.Libraries.Shared.Services.Http;

public interface IDownloadsApi
{
  Task<Result> DownloadDesktopClientZip(string destinationPath, string desktopClientDownloadUri, Func<double, Task>? onDownloadProgress);
  Task<Result> DownloadFile(string downloadUri, string destinationPath, CancellationToken cancellationToken = default);
  Task<Result> DownloadFile(Uri downloadUri, string destinationPath, CancellationToken cancellationToken = default);

}

public class DownloadsApi(
    HttpClient client,
    ILogger<DownloadsApi> logger) : IDownloadsApi
{
  private readonly HttpClient _client = client;
  private readonly ILogger<DownloadsApi> _logger = logger;

  public async Task<Result> DownloadDesktopClientZip(string destinationPath, string desktopClientDownloadUri, Func<double, Task>? onDownloadProgress)
  {
    try
    {
      using var message = new HttpRequestMessage(HttpMethod.Head, desktopClientDownloadUri);
      using var response = await _client.SendAsync(message);
      var totalSize = response.Content.Headers.ContentLength ?? 100_000_000; // rough estimate.

      await using var webStream = await _client.GetStreamAsync(desktopClientDownloadUri);
      await using var fs = new ReactiveFileStream(destinationPath, FileMode.Create);

      fs.TotalBytesWrittenChanged += async (_, written) =>
      {
        if (onDownloadProgress is not null)
        {
          var progress = (double)written / totalSize;
          await onDownloadProgress.Invoke(progress);
        }
      };

      await webStream.CopyToAsync(fs);

      if (onDownloadProgress is not null)
      {
        await onDownloadProgress.Invoke(1);
      }

      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while downloading remote control client.");
      return Result.Fail(ex);
    }
  }

  public async Task<Result> DownloadFile(string downloadUri, string destinationPath, CancellationToken cancellationToken = default)
  {
    try
    {
      await using var webStream = await _client.GetStreamAsync(downloadUri, cancellationToken);
      await using var fs = new FileStream(destinationPath, FileMode.Create);
      await webStream.CopyToAsync(fs, cancellationToken);
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while downloading file {DownloadUri}.", downloadUri);
      return Result.Fail(ex);
    }
  }

  public Task<Result> DownloadFile(Uri downloadUri, string destinationPath, CancellationToken cancellationToken = default)
  {
    return DownloadFile($"{downloadUri}", destinationPath, cancellationToken);
  }

}