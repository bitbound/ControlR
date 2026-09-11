using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using DeviceTagsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceTags;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult> IDeviceTagsApi.AddDeviceTag(Guid tenantId, DeviceTagsDtos.DeviceTagAddRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.DeviceTagsEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IDeviceTagsApi.RemoveDeviceTag(Guid deviceId, Guid tagId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.DeviceTagsEndpoint}/{deviceId}/{tagId}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}
