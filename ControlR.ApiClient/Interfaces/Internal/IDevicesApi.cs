using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IDevicesApi
{
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/{{deviceId}}", "DELETE")]
  Task<ApiResult> DeleteDevice(Guid deviceId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/delete-many", "POST")]
  Task<ApiResult<DeleteManyDevicesResponseDto>> DeleteManyDevices(DeleteDevicesRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}", "GET")]
  IAsyncEnumerable<DeviceResponseDto> GetAllDevices(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/{{deviceId}}", "GET")]
  Task<ApiResult<DeviceResponseDto>> GetDevice(Guid deviceId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/summary", "GET")]
  IAsyncEnumerable<DeviceSummaryDto> GetDeviceSummaries(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/search", "POST")]
  Task<ApiResult<DeviceSearchResponseDto>> SearchDevices(DeviceSearchRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/{{deviceId}}/alias", "PATCH")]
  Task<ApiResult<DeviceResponseDto>> UpdateDeviceAlias(UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken = default);
}
