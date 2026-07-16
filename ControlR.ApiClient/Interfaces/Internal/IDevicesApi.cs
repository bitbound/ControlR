using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IDevicesApi
{
  [ApiRoute("DELETE", "/api/internal/devices/{deviceId}")]
  Task<ApiResult> DeleteDevice(Guid deviceId, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/devices/delete-many")]
  Task<ApiResult<InternalDtos.DeleteManyDevicesResponseDto>> DeleteManyDevices(InternalDtos.DeleteDevicesRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/devices")]
  IAsyncEnumerable<InternalDtos.DeviceResponseDto> GetAllDevices(CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/devices/{deviceId}")]
  Task<ApiResult<InternalDtos.DeviceResponseDto>> GetDevice(Guid deviceId, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/devices/summary")]
  IAsyncEnumerable<InternalDtos.DeviceSummaryDto> GetDeviceSummaries(CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/devices/search")]
  Task<ApiResult<InternalDtos.DeviceSearchResponseDto>> SearchDevices(InternalDtos.DeviceSearchRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("PATCH", "/api/internal/devices/{deviceId}/alias")]
  Task<ApiResult<InternalDtos.DeviceResponseDto>> UpdateDeviceAlias(InternalDtos.UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken = default);
}
