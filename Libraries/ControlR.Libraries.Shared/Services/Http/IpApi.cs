using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Helpers;
using System.Net.Http.Json;

namespace ControlR.Libraries.Shared.Services.Http;

public interface IIpApi
{
    Task<Result<IpApiResponse>> GetIpInfo(string ipAddress);
}
public class IpApi(
    HttpClient _httpClient,
    ILogger<IpApi> _logger) : IIpApi
{
    private readonly Uri _baseUri = new("http://ip-api.com/json/");

    public async Task<Result<IpApiResponse>> GetIpInfo(string ipAddress)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return Result.Fail<IpApiResponse>("No IP address cannot be empty.").Log(_logger);
            }

            var response = await _httpClient.GetFromJsonAsync<IpApiResponse>($"{_baseUri}{ipAddress}");
            Guard.IsNotNull(response);

            return Result.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errror while getting IP location.");
            return Result.Fail<IpApiResponse>(ex, "Error while getting IP location.").Log(_logger);
        }
    }
}
