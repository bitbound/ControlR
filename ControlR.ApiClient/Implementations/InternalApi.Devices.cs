using System.Runtime.CompilerServices;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using Microsoft.Extensions.Logging;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult> IDevicesApi.CreateDevice(CreateDeviceRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.Internal.DevicesEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IDevicesApi.DeleteDevice(Guid deviceId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.Internal.DevicesEndpoint}/{deviceId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<DeleteManyDevicesResponseDto>> IDevicesApi.DeleteManyDevices(
    DeleteDevicesRequestDto request, CancellationToken cancellationToken)
  {
    if (request.DeviceIds is { Length: > DeleteDevicesRequestDto.MaxDeviceIds })
    {
      return ApiResult.Fail<DeleteManyDevicesResponseDto>(
        $"Too many device IDs: {request.DeviceIds.Length}. Maximum allowed is {DeleteDevicesRequestDto.MaxDeviceIds}.");
    }

    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.Internal.DevicesEndpoint}/delete-many", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content
        .ReadFromJsonAsync<DeleteManyDevicesResponseDto>(cancellationToken);
    });
  }

  async IAsyncEnumerable<DeviceResponseDto> IDevicesApi.GetAllDevices([EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var stream = _client.HttpClient.GetFromJsonAsAsyncEnumerable<DeviceResponseDto>(
      HttpConstants.Internal.DevicesEndpoint,
      cancellationToken: cancellationToken);

    await foreach (var device in stream.WithCancellation(cancellationToken))
    {
      if (device is null)
      {
        continue;
      }

      if (!_client.Options.Value.DisableStreamingResponseDtoStrictness)
      {
        var validationErrors = DtoValidatorFactory.Validate(device);
        if (validationErrors is not null)
        {
          if (_client.Options.Value.DisableResponseDtoStrictness)
          {
            _client.Logger.LogWarning("Streaming response DTO validation failed but strictness is disabled: {Reason}", validationErrors);
          }
          else
          {
            throw new InvalidDataException($"DTO validation failed: {validationErrors}");
          }
        }
      }

      yield return device;
    }
  }

  async Task<ApiResult<DeviceResponseDto>> IDevicesApi.GetDevice(Guid deviceId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<DeviceResponseDto>($"{HttpConstants.Internal.DevicesEndpoint}/{deviceId}", cancellationToken));
  }

  async IAsyncEnumerable<DeviceSummaryDto> IDevicesApi.GetDeviceSummaries([EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var stream = _client.HttpClient.GetFromJsonAsAsyncEnumerable<DeviceSummaryDto>(
      $"{HttpConstants.Internal.DevicesEndpoint}/summary",
      cancellationToken: cancellationToken);

    await foreach (var device in stream.WithCancellation(cancellationToken))
    {
      if (device is null)
      {
        continue;
      }

      if (!_client.Options.Value.DisableStreamingResponseDtoStrictness)
      {
        var validationErrors = DtoValidatorFactory.Validate(device);
        if (validationErrors is not null)
        {
          if (_client.Options.Value.DisableResponseDtoStrictness)
          {
            _client.Logger.LogWarning("Streaming response DTO validation failed but strictness is disabled: {Reason}", validationErrors);
          }
          else
          {
            throw new InvalidDataException($"DTO validation failed: {validationErrors}");
          }
        }
      }

      yield return device;
    }
  }

  async Task<ApiResult<DeviceSearchResponseDto>> IDevicesApi.SearchDevices(DeviceSearchRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.Internal.DevicesEndpoint}/search", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeviceSearchResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<DeviceResponseDto>> IDevicesApi.UpdateDeviceAlias(UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var content = JsonContent.Create(request);
      using var response = await _client.HttpClient.PatchAsync($"{HttpConstants.Internal.DevicesEndpoint}/{request.DeviceId}/alias", content, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeviceResponseDto>(cancellationToken);
    });
  }
}
