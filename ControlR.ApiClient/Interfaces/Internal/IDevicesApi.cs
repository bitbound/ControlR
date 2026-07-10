using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IDevicesApi
{
  Task<ApiResult> CreateDevice(InternalDtos.CreateDeviceRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> DeleteDevice(Guid deviceId, CancellationToken cancellationToken = default);
  Task<ApiResult<DeleteManyDevicesResponseDto>> DeleteManyDevices(DeleteDevicesRequestDto request, CancellationToken cancellationToken = default);
  IAsyncEnumerable<DeviceResponseDto> GetAllDevices(CancellationToken cancellationToken = default);
  Task<ApiResult<DeviceResponseDto>> GetDevice(Guid deviceId, CancellationToken cancellationToken = default);
  IAsyncEnumerable<DeviceSummaryDto> GetDeviceSummaries(CancellationToken cancellationToken = default);
  Task<ApiResult<DeviceSearchResponseDto>> SearchDevices(DeviceSearchRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<DeviceResponseDto>> UpdateDeviceAlias(UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken = default);
}
