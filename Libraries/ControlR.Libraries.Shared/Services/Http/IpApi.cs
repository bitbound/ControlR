using System.Net.Http.Json;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Libraries.Shared.Services.Http;

public interface IIpApi
{
  Task<Result<IpApiResponse>> GetIpInfo(string ipAddress);
}

public class IpApi(
  HttpClient httpClient,
  ILogger<IpApi> logger) : IIpApi
{
  private readonly Uri _baseUri = new("http://ip-api.com/json/");

  public async Task<Result<IpApiResponse>> GetIpInfo(string ipAddress)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(ipAddress))
      {
        return Result.Fail<IpApiResponse>("No IP address cannot be empty.").Log(logger);
      }

      var response = await httpClient.GetFromJsonAsync<IpApiResponse>($"{_baseUri}{ipAddress}");
      Guard.IsNotNull(response);

      return Result.Ok(response);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Errror while getting IP location.");
      return Result.Fail<IpApiResponse>(ex, "Error while getting IP location.").Log(logger);
    }
  }
}