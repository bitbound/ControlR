using ControlR.Libraries.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace ControlR.Libraries.Shared.Services.Http;

public interface IMeteredApi
{
    Task<IceServer[]> GetIceServers(string apiKey);
}

public class MeteredApi(HttpClient _httpClient) : IMeteredApi
{
    public async Task<IceServer[]> GetIceServers(string apiKey)
    {
        using var response = await _httpClient.GetAsync($"https://controlr.metered.live/api/v1/turn/credentials?apiKey={apiKey}");
        response.EnsureSuccessStatusCode();

        var servers = await response.Content.ReadFromJsonAsync<IceServer[]>();
        if (servers is null)
        {
            return [];
        }
        return servers
            .Select(x =>
                new IceServer()
                {
                    Credential = x.Credential,
                    CredentialType = "password",
                    Urls = x.Urls,
                    Username = x.Username
                })
            .ToArray();
    }
}
