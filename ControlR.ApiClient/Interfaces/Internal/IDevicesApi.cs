using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IDevicesApi
{
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/{{deviceId}}", "DELETE")]
  Task<ApiResult> DeleteDevice(Guid deviceId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/delete-many", "POST")]
  [Obsolete("Use ControlrApi.V1.Devices.DeleteManyDevices (POST /api/v1/devices/delete-many), which returns the same value. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<DeleteManyDevicesResponseDto>> DeleteManyDevices(DeleteDevicesRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}", "GET")]
  IAsyncEnumerable<DeviceResponseDto> GetAllDevices(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/{{deviceId}}", "GET")]
  Task<ApiResult<DeviceResponseDto>> GetDevice(Guid deviceId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/summary", "GET")]
  [Obsolete("Use ControlrApi.V1.Devices.GetDeviceSummaries (GET /api/v1/devices/summary), which returns the same value. This internal route is unversioned and slated for removal.")]
  IAsyncEnumerable<DeviceSummaryDto> GetDeviceSummaries(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/search", "POST")]
  Task<ApiResult<DeviceSearchResponseDto>> SearchDevices(DeviceSearchRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DevicesEndpoint}/{{deviceId}}/alias", "PATCH")]
  Task<ApiResult<DeviceResponseDto>> UpdateDeviceAlias(UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken = default);
}
