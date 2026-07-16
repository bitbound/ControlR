using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using V0Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient.Interfaces.V0;

public interface IDevicesApi
{
  [ApiRoute("DELETE", "/api/v0/devices/{deviceId}")]
  Task<ApiResult> DeleteDevice(Guid deviceId, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/v0/devices/delete-many")]
  Task<ApiResult<V0Dtos.DeleteManyDevicesResponseDto>> DeleteManyDevices(V0Dtos.DeleteDevicesRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/v0/devices")]
  IAsyncEnumerable<V0Dtos.DeviceResponseDto> GetAllDevices(CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/v0/devices/{deviceId}")]
  Task<ApiResult<V0Dtos.DeviceResponseDto>> GetDevice(Guid deviceId, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/v0/devices/summary")]
  IAsyncEnumerable<V0Dtos.DeviceSummaryDto> GetDeviceSummaries(CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/v0/devices/search")]
  Task<ApiResult<V0Dtos.DeviceSearchResponseDto>> SearchDevices(V0Dtos.DeviceSearchRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("PATCH", "/api/v0/devices/{deviceId}/alias")]
  Task<ApiResult<V0Dtos.DeviceResponseDto>> UpdateDeviceAlias(V0Dtos.UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken = default);
}
