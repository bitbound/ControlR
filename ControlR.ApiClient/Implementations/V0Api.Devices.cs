using System.Runtime.CompilerServices;
using ControlR.ApiClient.Interfaces.V0;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using V0Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient;

internal partial class V0Api
{
  async Task<ApiResult> IDevicesApi.DeleteDevice(Guid deviceId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.V0.DevicesEndpoint}/{deviceId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<V0Dtos.DeleteManyDevicesResponseDto>> IDevicesApi.DeleteManyDevices(
    V0Dtos.DeleteDevicesRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V0.DevicesEndpoint}/delete-many", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content
        .ReadFromJsonAsync<V0Dtos.DeleteManyDevicesResponseDto>(cancellationToken);
    });
  }

  async IAsyncEnumerable<V0Dtos.DeviceResponseDto> IDevicesApi.GetAllDevices([EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var stream = _client.HttpClient.GetFromJsonAsAsyncEnumerable<V0Dtos.DeviceResponseDto>(
      HttpConstants.V0.DevicesEndpoint,
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

  async Task<ApiResult<V0Dtos.DeviceResponseDto>> IDevicesApi.GetDevice(Guid deviceId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.V0.DevicesEndpoint}/{deviceId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<V0Dtos.DeviceResponseDto>(cancellationToken);
    });
  }

  async IAsyncEnumerable<V0Dtos.DeviceSummaryDto> IDevicesApi.GetDeviceSummaries([EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var stream = _client.HttpClient.GetFromJsonAsAsyncEnumerable<V0Dtos.DeviceSummaryDto>(
      $"{HttpConstants.V0.DevicesEndpoint}/summary",
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

  async Task<ApiResult<V0Dtos.DeviceSearchResponseDto>> IDevicesApi.SearchDevices(V0Dtos.DeviceSearchRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.V0.DevicesEndpoint}/search", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<V0Dtos.DeviceSearchResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<V0Dtos.DeviceResponseDto>> IDevicesApi.UpdateDeviceAlias(V0Dtos.UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var content = JsonContent.Create(request);
      using var response = await _client.HttpClient.PatchAsync($"{HttpConstants.V0.DevicesEndpoint}/{request.DeviceId}/alias", content, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<V0Dtos.DeviceResponseDto>(cancellationToken);
    });
  }
}
