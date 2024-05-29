using ControlR.Shared.Dtos.GitHubDtos;
using ControlR.Shared.Helpers;
using ControlR.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace ControlR.Shared.Services.Http;
public interface IGitHubApi
{
    Task<Result<GitHubAsset>> GetLatestAgentHash(RuntimeId runtimeId);
}
internal class GitHubApi : IGitHubApi
{
    private const string _latestReleasePath = "/repos/controlr/bitbound/releases/latest";
    private readonly HttpClient _client;

    private readonly ILogger<GitHubApi> _logger;

    public GitHubApi(
        HttpClient client,
        ILogger<GitHubApi> logger)
    {
        client.BaseAddress = new Uri("https://api.github.com");
        _client = client;
        _logger = logger;
    }

    public async Task<Result<GitHubAsset>> GetLatestAgentHash(RuntimeId runtimeId)
    {
        try
        {
            var assetName = runtimeId switch
            {
                RuntimeId.WinX64 or RuntimeId.WinX86 => "ControlR.Agent.exe",
                RuntimeId.LinuxX64 => "ControlR.Agent",
                _ => throw new PlatformNotSupportedException()
            };

            var release = await _client.GetFromJsonAsync<Release>(_latestReleasePath);
            if (release is null)
            {
                return Result
                    .Fail<GitHubAsset>("Release response is empty.")
                    .Log(_logger);
            }

            var asset = release.Assets.FirstOrDefault(x => x.Name == assetName);
            if (asset is null)
            {
                return Result
                    .Fail<GitHubAsset>("Asset named {AssetName} not found in release.")
                    .Log(_logger);
            }

            Guard.IsNotNullOrWhiteSpace(asset.BrowserDownloadUrl);

            using var request = new HttpRequestMessage(HttpMethod.Head, asset.BrowserDownloadUrl);
            using var response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentMD5 is not byte[] contentMd5)
            {
                return Result
                    .Fail<GitHubAsset>($"ContentMD5 was empty for asset {asset.BrowserDownloadUrl}.")
                    .Log(_logger);
            }

            return Result.Ok(new GitHubAsset(contentMd5, asset.BrowserDownloadUrl));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting latest agent hash for runtime ID {RID}.", runtimeId);
            return Result.Fail<GitHubAsset>(ex).Log(_logger);
        }
    }
}