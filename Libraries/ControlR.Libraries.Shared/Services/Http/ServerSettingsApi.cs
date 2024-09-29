using ControlR.Libraries.Shared.Dtos.ServerApi;
using System.Net.Http.Json;

namespace ControlR.Libraries.Shared.Services.Http;

public interface IServerSettingsApi
{
  Task<Result<ServerSettingsDto>> GetServerSettings();
}

public class ServerSettingsApi(
  HttpClient httpClient,
  ILogger<ServerSettingsApi> logger) : IServerSettingsApi
{
  private readonly HttpClient _client = httpClient;
  private readonly ILogger<ServerSettingsApi> _logger = logger;
  private readonly string _serverSettingsEndpoint = "/api/server-settings";

  public async Task<Result<ServerSettingsDto>> GetServerSettings()
  {
    try
    {
      var serverSettings = await _client.GetFromJsonAsync<ServerSettingsDto>(_serverSettingsEndpoint);
      if (serverSettings is null)
      {
        return Result.Fail<ServerSettingsDto>("Server settings response was empty.");
      }

      return Result.Ok(serverSettings);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<ServerSettingsDto>(ex, "Error while getting server settings.")
        .Log(_logger);
    }
  }

}
