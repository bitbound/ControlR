using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using V0Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient.Interfaces.V0;

public interface IV0DevicesApi
{
  Task<ApiResult> CreateDevice(V0Dtos.CreateDeviceRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> DeleteDevice(Guid deviceId, CancellationToken cancellationToken = default);
  Task<ApiResult<DeleteManyDevicesResponseDto>> DeleteManyDevices(DeleteDevicesRequestDto request, CancellationToken cancellationToken = default);
  IAsyncEnumerable<DeviceResponseDto> GetAllDevices(CancellationToken cancellationToken = default);
  Task<ApiResult<DeviceResponseDto>> GetDevice(Guid deviceId, CancellationToken cancellationToken = default);
  IAsyncEnumerable<DeviceSummaryDto> GetDeviceSummaries(CancellationToken cancellationToken = default);
  Task<ApiResult<DeviceSearchResponseDto>> SearchDevices(DeviceSearchRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<DeviceResponseDto>> UpdateDeviceAlias(UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken = default);
}
