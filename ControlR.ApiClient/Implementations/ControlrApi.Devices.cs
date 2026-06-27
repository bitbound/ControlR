using System.Runtime.CompilerServices;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using Microsoft.Extensions.Logging;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult> IDevicesApi.CreateDevice(CreateDeviceRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.DevicesEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IDevicesApi.DeleteDevice(Guid deviceId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.DevicesEndpoint}/{deviceId}", cancellationToken);
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

    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(
        $"{HttpConstants.DevicesEndpoint}/delete-many", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content
        .ReadFromJsonAsync<DeleteManyDevicesResponseDto>(cancellationToken);
    });
  }

  async IAsyncEnumerable<DeviceResponseDto> IDevicesApi.GetAllDevices([EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var stream = _client.GetFromJsonAsAsyncEnumerable<DeviceResponseDto>(
      HttpConstants.DevicesEndpoint,
      cancellationToken: cancellationToken);

    await foreach (var device in stream.WithCancellation(cancellationToken))
    {
      if (device is null)
      {
        continue;
      }

      if (!_options.Value.DisableStreamingResponseDtoStrictness)
      {
        var validationErrors = DtoValidatorFactory.Validate(device);
        if (validationErrors is not null)
        {
          if (_options.Value.DisableResponseDtoStrictness)
          {
            _logger.LogWarning("Streaming response DTO validation failed but strictness is disabled: {Reason}", validationErrors);
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
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<DeviceResponseDto>($"{HttpConstants.DevicesEndpoint}/{deviceId}", cancellationToken));
  }

  async IAsyncEnumerable<DeviceSummaryDto> IDevicesApi.GetDeviceSummaries([EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var stream = _client.GetFromJsonAsAsyncEnumerable<DeviceSummaryDto>(
      $"{HttpConstants.DevicesEndpoint}/summary",
      cancellationToken: cancellationToken);

    await foreach (var device in stream.WithCancellation(cancellationToken))
    {
      if (device is null)
      {
        continue;
      }

      if (!_options.Value.DisableStreamingResponseDtoStrictness)
      {
        var validationErrors = DtoValidatorFactory.Validate(device);
        if (validationErrors is not null)
        {
          if (_options.Value.DisableResponseDtoStrictness)
          {
            _logger.LogWarning("Streaming response DTO validation failed but strictness is disabled: {Reason}", validationErrors);
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
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.DevicesEndpoint}/search", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeviceSearchResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<DeviceResponseDto>> IDevicesApi.UpdateDeviceAlias(UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var content = JsonContent.Create(request);
      using var response = await _client.PatchAsync($"{HttpConstants.DevicesEndpoint}/{request.DeviceId}/alias", content, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeviceResponseDto>(cancellationToken);
    });
  }
}
