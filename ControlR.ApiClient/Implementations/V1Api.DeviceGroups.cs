using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using DGDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceGroups;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult> IDeviceGroupsApi.AddDeviceGroupMembers(Guid deviceGroupId, Guid tenantId, DGDtos.AddDeviceGroupMembersRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.DeviceGroupsEndpoint}/{deviceGroupId}/members?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<DGDtos.DeviceGroupDetailDto>> IDeviceGroupsApi.CreateDeviceGroup(Guid tenantId, DGDtos.CreateDeviceGroupRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.DeviceGroupsEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DGDtos.DeviceGroupDetailDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IDeviceGroupsApi.DeleteDeviceGroup(Guid deviceGroupId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.DeviceGroupsEndpoint}/{deviceGroupId}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<DGDtos.DeviceGroupDetailDto>> IDeviceGroupsApi.GetDeviceGroup(Guid deviceGroupId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<DGDtos.DeviceGroupDetailDto>(
        $"{HttpConstants.V1.DeviceGroupsEndpoint}/{deviceGroupId}?tenantId={tenantId}", cancellationToken));
  }

  async Task<ApiResult<DGDtos.DeviceGroupsResponseDto>> IDeviceGroupsApi.GetAllDeviceGroups(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<DGDtos.DeviceGroupsResponseDto>(
        $"{HttpConstants.V1.DeviceGroupsEndpoint}?tenantId={tenantId}", cancellationToken));
  }

  async Task<ApiResult> IDeviceGroupsApi.RemoveDeviceGroupMembers(Guid deviceGroupId, Guid tenantId, DGDtos.RemoveDeviceGroupMembersRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
        $"{HttpConstants.V1.DeviceGroupsEndpoint}/{deviceGroupId}/members?tenantId={tenantId}")
      {
        Content = JsonContent.Create(request)
      }, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<DGDtos.DeviceGroupDetailDto>> IDeviceGroupsApi.UpdateDeviceGroup(Guid deviceGroupId, Guid tenantId, DGDtos.UpdateDeviceGroupRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.V1.DeviceGroupsEndpoint}/{deviceGroupId}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DGDtos.DeviceGroupDetailDto>(cancellationToken);
    });
  }
}