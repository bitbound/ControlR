using ControlR.Libraries.Shared.IO;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.Shared.Services.Http;

internal interface IDownloadsApi
{
    Task<Result> DownloadFile(string downloadUri, string destinationPath);

    Task<Result> DownloadStreamerZip(string destinationPath, string streamerDownloadUri, Func<double, Task>? onDownloadProgress);

}

internal class DownloadsApi(
    HttpClient client,
    ILogger<DownloadsApi> logger) : IDownloadsApi
{
    private readonly HttpClient _client = client;
    private readonly ILogger<DownloadsApi> _logger = logger;

    public async Task<Result> DownloadFile(string downloadUri, string destinationPath)
    {
        try
        {
            using var webStream = await _client.GetStreamAsync(downloadUri);
            using var fs = new FileStream(destinationPath, FileMode.Create);
            await webStream.CopyToAsync(fs);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while downloading file {DownloadUri}.", downloadUri);
            return Result.Fail(ex);
        }
    }

    public async Task<Result> DownloadStreamerZip(string destinationPath, string streamerDownloadUri, Func<double, Task>? onDownloadProgress)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Head, streamerDownloadUri);
            using var response = await _client.SendAsync(message);
            var totalSize = response.Content.Headers.ContentLength ?? 100_000_000; // rough estimate.

            using var webStream = await _client.GetStreamAsync(streamerDownloadUri);
            using var fs = new ReactiveFileStream(destinationPath, FileMode.Create);

            fs.TotalBytesWrittenChanged += async (sender, written) =>
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

}