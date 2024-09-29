using ControlR.Libraries.Shared.Dtos.ServerApi;
using System.Net.Http.Json;

namespace ControlR.Libraries.Shared.Services.Http;

public interface IDeviceGroupsApi
{
  Task<Result<List<DeviceGroupDto>>> GetAllDeviceGroups();
}

public class DeviceGroupsApi(
  HttpClient httpClient,
  ILogger<DeviceGroupsApi> logger) : IDeviceGroupsApi
{
  private readonly HttpClient _client = httpClient;
  private readonly ILogger<DeviceGroupsApi> _logger = logger;
  private readonly string _serverSettingsEndpoint = "/api/device-groups";

  public async Task<Result<List<DeviceGroupDto>>> GetAllDeviceGroups()
  {
    try
    {
      var deviceGroups = await _client.GetFromJsonAsync<List<DeviceGroupDto>>(_serverSettingsEndpoint);
      if (deviceGroups is null)
      {
        return Result.Fail<List<DeviceGroupDto>>("Server response was empty.");
      }

      return Result.Ok(deviceGroups);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<List<DeviceGroupDto>>(ex, "Error while getting device groups.")
        .Log(_logger);
    }
  }

}
