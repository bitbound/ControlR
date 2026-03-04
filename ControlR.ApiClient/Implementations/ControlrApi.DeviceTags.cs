using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult> IDeviceTagsApi.AddDeviceTag(DeviceTagAddRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.DeviceTagsEndpoint}", request, cancellationToken);
      response.EnsureSuccessStatusCode();
    });
  }

  async Task<ApiResult> IDeviceTagsApi.RemoveDeviceTag(Guid deviceId, Guid tagId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.DeviceTagsEndpoint}/{deviceId}/{tagId}", cancellationToken);
      response.EnsureSuccessStatusCode();
    });
  }
}
