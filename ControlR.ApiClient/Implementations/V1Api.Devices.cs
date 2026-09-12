using System.Runtime.CompilerServices;
using ControlR.ApiClient.Interfaces.V1;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

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

  async Task<ApiResult<DeleteManyDevicesResponseDto>> IDevicesApi.DeleteManyDevices(
    DeleteDevicesRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.DevicesEndpoint}/delete-many", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content
        .ReadFromJsonAsync<DeleteManyDevicesResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<DesktopSessionsResponseDto>> IDevicesApi.GetActiveDesktopSessions(
    Guid deviceId, 
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync(
        $"{HttpConstants.V1.DevicesEndpoint}/{deviceId}/desktop-sessions",
        cancellationToken);

      await response.EnsureSuccessStatusCodeWithDetails();

      return await response.Content
        .ReadFromJsonAsync<DesktopSessionsResponseDto>(cancellationToken);
    });
  }

  async IAsyncEnumerable<DeviceResponseDto> IDevicesApi.GetAllDevices([EnumeratorCancellation] CancellationToken cancellationToken)
  {
    using var tracked = _client.BeginTrackedRequest();

    var stream = _client.HttpClient.GetFromJsonAsAsyncEnumerable<DeviceResponseDto>(
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

  async Task<ApiResult<DeviceResponseDto>> IDevicesApi.GetDevice(Guid deviceId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.V1.DevicesEndpoint}/{deviceId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeviceResponseDto>(cancellationToken);
    });
  }

  async IAsyncEnumerable<DeviceSummaryDto> IDevicesApi.GetDeviceSummaries([EnumeratorCancellation] CancellationToken cancellationToken)
  {
    using var tracked = _client.BeginTrackedRequest();

    var stream = _client.HttpClient.GetFromJsonAsAsyncEnumerable<DeviceSummaryDto>(
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

  async Task<ApiResult<DeviceSearchResponseDto>> IDevicesApi.SearchDevices(DeviceSearchRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.V1.DevicesEndpoint}/search", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeviceSearchResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<DeviceResponseDto>> IDevicesApi.UpdateDeviceAlias(UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var content = JsonContent.Create(request);
      using var response = await _client.HttpClient.PatchAsync($"{HttpConstants.V1.DevicesEndpoint}/{request.DeviceId}/alias", content, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeviceResponseDto>(cancellationToken);
    });
  }
}
