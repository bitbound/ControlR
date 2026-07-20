using System.Runtime.CompilerServices;
using ControlR.ApiClient.Interfaces.V1;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using V1Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult> IDevicesApi.DeleteDevice(Guid deviceId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.V1.DevicesEndpoint}/{deviceId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<V1Dtos.DeleteManyDevicesResponseDto>> IDevicesApi.DeleteManyDevices(
    V1Dtos.DeleteDevicesRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.DevicesEndpoint}/delete-many", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content
        .ReadFromJsonAsync<V1Dtos.DeleteManyDevicesResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<List<V1Dtos.DesktopSessionResponseDto>>> IDevicesApi.GetActiveDesktopSessions(
    Guid deviceId, 
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync(
        $"{HttpConstants.V1.DevicesEndpoint}/{deviceId}/desktop-sessions",
        cancellationToken);

      await response.EnsureSuccessStatusCodeWithDetails();

      var list = await response.Content
        .ReadFromJsonAsync<List<V1Dtos.DesktopSessionResponseDto>>(cancellationToken);

      return list ?? [];
    });
  }

  async IAsyncEnumerable<V1Dtos.DeviceResponseDto> IDevicesApi.GetAllDevices([EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var stream = _client.HttpClient.GetFromJsonAsAsyncEnumerable<V1Dtos.DeviceResponseDto>(
      HttpConstants.V1.DevicesEndpoint,
      cancellationToken: cancellationToken);

    await foreach (var device in stream.WithCancellation(cancellationToken))
    {
      if (device is null)
      {
        continue;
      }

      yield return device;
    }
  }

  async Task<ApiResult<V1Dtos.DeviceResponseDto>> IDevicesApi.GetDevice(Guid deviceId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.V1.DevicesEndpoint}/{deviceId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<V1Dtos.DeviceResponseDto>(cancellationToken);
    });
  }

  async IAsyncEnumerable<V1Dtos.DeviceSummaryDto> IDevicesApi.GetDeviceSummaries([EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var stream = _client.HttpClient.GetFromJsonAsAsyncEnumerable<V1Dtos.DeviceSummaryDto>(
      $"{HttpConstants.V1.DevicesEndpoint}/summary",
      cancellationToken: cancellationToken);

    await foreach (var device in stream.WithCancellation(cancellationToken))
    {
      if (device is null)
      {
        continue;
      }

      yield return device;
    }
  }

  async Task<ApiResult<V1Dtos.DeviceSearchResponseDto>> IDevicesApi.SearchDevices(V1Dtos.DeviceSearchRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.V1.DevicesEndpoint}/search", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<V1Dtos.DeviceSearchResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<V1Dtos.DeviceResponseDto>> IDevicesApi.UpdateDeviceAlias(V1Dtos.UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var content = JsonContent.Create(request);
      using var response = await _client.HttpClient.PatchAsync($"{HttpConstants.V1.DevicesEndpoint}/{request.DeviceId}/alias", content, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<V1Dtos.DeviceResponseDto>(cancellationToken);
    });
  }
}
