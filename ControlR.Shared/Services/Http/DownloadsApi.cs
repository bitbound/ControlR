﻿using ControlR.Shared.Primitives;
using Microsoft.Extensions.Logging;

namespace ControlR.Shared.Services.Http;

internal interface IDownloadsApi
{
    Task<Result> DownloadAgent(string destinationPath, string agentDownloadUri);

    Task<Result> DownloadTightVncZip(string destinationPath);

    Task<Result> DownloadViewer(string destinationPath, string viewerDownloadUri);

    Task<Result<string>> GetAgentEtag(string agentDownloadUri);
}

internal class DownloadsApi(
    HttpClient client,
    ILogger<DownloadsApi> logger) : IDownloadsApi
{
    private readonly HttpClient _client = client;
    private readonly ILogger<DownloadsApi> _logger = logger;

    public async Task<Result> DownloadAgent(string destinationPath, string agentDownloadUri)
    {
        try
        {
            using var webStream = await _client.GetStreamAsync(agentDownloadUri);
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

    public async Task<Result> DownloadTightVncZip(string destinationPath)
    {
        try
        {
            var fileUrl = $"{AppConstants.ExternalDownloadsUri}/downloads/{AppConstants.TightVncZipName}";

            using var webStream = await _client.GetStreamAsync(fileUrl);

            using var fs = new FileStream(destinationPath, FileMode.Create);

            await webStream.CopyToAsync(fs);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while downloading TightVNC.");
            return Result.Fail(ex);
        }
    }

    public async Task<Result> DownloadViewer(string destinationPath, string viewerDownloadUri)
    {
        try
        {
            using var webStream = await _client.GetStreamAsync(viewerDownloadUri);
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

    public async Task<Result<string>> GetAgentEtag(string agentDownloadUri)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, agentDownloadUri);

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