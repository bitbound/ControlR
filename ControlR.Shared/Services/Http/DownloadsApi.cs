using Microsoft.Extensions.Logging;

namespace ControlR.Shared.Services.Http;

internal interface IDownloadsApi
{
    Task<Result> DownloadAgent(string destinationPath);

    Task<Result> DownloadRemoteControl(string destinationPath);

    Task<Result> DownloadVncZipFile(string destinationPath);

    Task<Result<string>> GetAgentEtag();
}

internal class DownloadsApi(
    HttpClient client,
    ILogger<DownloadsApi> logger) : IDownloadsApi
{
    private readonly HttpClient _client = client;
    private readonly ILogger<DownloadsApi> _logger = logger;

    public async Task<Result> DownloadAgent(string destinationPath)
    {
        try
        {
            using var webStream = await _client.GetStreamAsync($"{AppConstants.ServerUri}/downloads/{AppConstants.AgentFileName}");
            using var fs = new FileStream(destinationPath, FileMode.Create);
            await webStream.CopyToAsync(fs);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while downloading agent.");
            return Result.Fail(ex);
        }
    }

    public async Task<Result> DownloadRemoteControl(string destinationPath)
    {
        try
        {
            using var webStream = await _client.GetStreamAsync($"{AppConstants.ServerUri}/downloads/{AppConstants.VncFileName}");
            using var fs = new FileStream(destinationPath, FileMode.Create);
            await webStream.CopyToAsync(fs);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while downloading remote control client.");
            return Result.Fail(ex);
        }
    }

    public async Task<Result> DownloadVncZipFile(string destinationPath)
    {
        try
        {
            var zipUrl = $"{AppConstants.DownloadsUri}/downloads/{AppConstants.VncZipFileName}";

            using var message = new HttpRequestMessage(HttpMethod.Head, zipUrl);
            using var response = await _client.SendAsync(message);

            using var webStream = await _client.GetStreamAsync(zipUrl);

            using var fs = new FileStream(destinationPath, FileMode.Create);

            await webStream.CopyToAsync(fs);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while downloading remote control client.");
            return Result.Fail(ex);
        }
    }

    public async Task<Result<string>> GetAgentEtag()
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Head,
                $"{AppConstants.ServerUri}/downloads/{AppConstants.AgentFileName}");

            using var response = await _client.SendAsync(request);
            var etag = response.Headers.ETag?.Tag;

            if (string.IsNullOrWhiteSpace(etag))
            {
                return Result.Fail<string>("Etag from HEAD request is empty.");
            }

            return Result.Ok(etag);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking agent etag.");
            return Result.Fail<string>(ex);
        }
    }
}