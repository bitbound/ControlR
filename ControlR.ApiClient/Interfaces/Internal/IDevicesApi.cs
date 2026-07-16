using ControlR.Libraries.Api.Contracts.Dtos;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IDevicesApi
{
  
  Task<ApiResult> DeleteDevice(Guid deviceId, CancellationToken cancellationToken = default);
  Task<ApiResult<InternalDtos.DeleteManyDevicesResponseDto>> DeleteManyDevices(InternalDtos.DeleteDevicesRequestDto request, CancellationToken cancellationToken = default);
  IAsyncEnumerable<InternalDtos.DeviceResponseDto> GetAllDevices(CancellationToken cancellationToken = default);
  Task<ApiResult<InternalDtos.DeviceResponseDto>> GetDevice(Guid deviceId, CancellationToken cancellationToken = default);
  IAsyncEnumerable<InternalDtos.DeviceSummaryDto> GetDeviceSummaries(CancellationToken cancellationToken = default);
  Task<ApiResult<InternalDtos.DeviceSearchResponseDto>> SearchDevices(InternalDtos.DeviceSearchRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<InternalDtos.DeviceResponseDto>> UpdateDeviceAlias(InternalDtos.UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken = default);
}
