using System.Runtime.CompilerServices;
using ControlR.ApiClient.Interfaces.V0;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using V0Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient;

internal partial class V0Api
{
  async Task<ApiResult> IV0DevicesApi.CreateDevice(V0Dtos.CreateDeviceRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.V0.DevicesEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IV0DevicesApi.DeleteDevice(Guid deviceId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.V0.DevicesEndpoint}/{deviceId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<DeleteManyDevicesResponseDto>> IV0DevicesApi.DeleteManyDevices(
    DeleteDevicesRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V0.DevicesEndpoint}/delete-many", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content
        .ReadFromJsonAsync<DeleteManyDevicesResponseDto>(cancellationToken);
    });
  }

  async IAsyncEnumerable<DeviceResponseDto> IV0DevicesApi.GetAllDevices([EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var stream = _client.HttpClient.GetFromJsonAsAsyncEnumerable<DeviceResponseDto>(
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

  async Task<ApiResult<DeviceResponseDto>> IV0DevicesApi.GetDevice(Guid deviceId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<DeviceResponseDto>($"{HttpConstants.V0.DevicesEndpoint}/{deviceId}", cancellationToken));
  }

  async IAsyncEnumerable<DeviceSummaryDto> IV0DevicesApi.GetDeviceSummaries([EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var stream = _client.HttpClient.GetFromJsonAsAsyncEnumerable<DeviceSummaryDto>(
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

  async Task<ApiResult<DeviceSearchResponseDto>> IV0DevicesApi.SearchDevices(DeviceSearchRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.V0.DevicesEndpoint}/search", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeviceSearchResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<DeviceResponseDto>> IV0DevicesApi.UpdateDeviceAlias(UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var content = JsonContent.Create(request);
      using var response = await _client.HttpClient.PatchAsync($"{HttpConstants.V0.DevicesEndpoint}/{request.DeviceId}/alias", content, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeviceResponseDto>(cancellationToken);
    });
  }
}
