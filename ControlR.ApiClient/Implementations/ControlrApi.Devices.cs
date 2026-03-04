using System.Runtime.CompilerServices;
using System.Net.Http.Json;
using System.IO;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
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
      response.EnsureSuccessStatusCode();
    });
  }

  async Task<ApiResult> IDevicesApi.DeleteDevice(Guid deviceId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.DevicesEndpoint}/{deviceId}", cancellationToken);
      response.EnsureSuccessStatusCode();
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

  async Task<ApiResult<DeviceSearchResponseDto>> IDevicesApi.SearchDevices(DeviceSearchRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.DevicesEndpoint}/search", request, cancellationToken);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<DeviceSearchResponseDto>(cancellationToken);
    });
  }
}
