using Microsoft.Extensions.Logging;
using System.Net.Sockets;

namespace ControlR.Libraries.Shared.Services.Http;

public interface IKeyApi
{
    Task<Result> VerifyKeys();
}

internal class KeyApi(
    HttpClient client,
    ILogger<KeyApi> logger) : IKeyApi
{
    private readonly HttpClient _client = client;
    private readonly ILogger<KeyApi> _logger = logger;

    public async Task<Result> VerifyKeys()
    {
        try
        {
            using var response = await _client.GetAsync($"/api/key/verify");
            response.EnsureSuccessStatusCode();
            return Result.Ok();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Key API unavailable due to HTTP exception.");
            return Result.Fail(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while verifying authentication.");
            return Result.Fail(ex);
        }
    }
}