using ControlR.Libraries.Api.Contracts.Dtos;
using V0Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient.Interfaces.V0;

public interface IV0DevicesApi
{
  Task<ApiResult> DeleteDevice(Guid deviceId, CancellationToken cancellationToken = default);
  Task<ApiResult<V0Dtos.DeleteManyDevicesResponseDto>> DeleteManyDevices(V0Dtos.DeleteDevicesRequestDto request, CancellationToken cancellationToken = default);
  IAsyncEnumerable<V0Dtos.DeviceResponseDto> GetAllDevices(CancellationToken cancellationToken = default);
  Task<ApiResult<V0Dtos.DeviceResponseDto>> GetDevice(Guid deviceId, CancellationToken cancellationToken = default);
  IAsyncEnumerable<V0Dtos.DeviceSummaryDto> GetDeviceSummaries(CancellationToken cancellationToken = default);
  Task<ApiResult<V0Dtos.DeviceSearchResponseDto>> SearchDevices(V0Dtos.DeviceSearchRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<V0Dtos.DeviceResponseDto>> UpdateDeviceAlias(V0Dtos.UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken = default);
}
