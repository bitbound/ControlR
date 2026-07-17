using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IDevicesApi
{
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/{{deviceId}}", "DELETE")]
  Task<ApiResult> DeleteDevice(Guid deviceId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/delete-many", "POST")]
  Task<ApiResult<InternalDtos.DeleteManyDevicesResponseDto>> DeleteManyDevices(InternalDtos.DeleteDevicesRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}", "GET")]
  IAsyncEnumerable<InternalDtos.DeviceResponseDto> GetAllDevices(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/{{deviceId}}", "GET")]
  Task<ApiResult<InternalDtos.DeviceResponseDto>> GetDevice(Guid deviceId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/summary", "GET")]
  IAsyncEnumerable<InternalDtos.DeviceSummaryDto> GetDeviceSummaries(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/search", "POST")]
  Task<ApiResult<InternalDtos.DeviceSearchResponseDto>> SearchDevices(InternalDtos.DeviceSearchRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/{{deviceId}}/alias", "PATCH")]
  Task<ApiResult<InternalDtos.DeviceResponseDto>> UpdateDeviceAlias(InternalDtos.UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken = default);
}
