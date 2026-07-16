using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using V0Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient.Interfaces.V0;

public interface IDevicesApi
{
  [ApiRoute($"{HttpConstants.V0.DevicesEndpoint}/{{deviceId}}", "DELETE")]
  Task<ApiResult> DeleteDevice(Guid deviceId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V0.DevicesEndpoint}/delete-many", "POST")]
  Task<ApiResult<V0Dtos.DeleteManyDevicesResponseDto>> DeleteManyDevices(V0Dtos.DeleteDevicesRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V0.DevicesEndpoint}", "GET")]
  IAsyncEnumerable<V0Dtos.DeviceResponseDto> GetAllDevices(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V0.DevicesEndpoint}/{{deviceId}}", "GET")]
  Task<ApiResult<V0Dtos.DeviceResponseDto>> GetDevice(Guid deviceId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V0.DevicesEndpoint}/summary", "GET")]
  IAsyncEnumerable<V0Dtos.DeviceSummaryDto> GetDeviceSummaries(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V0.DevicesEndpoint}/search", "POST")]
  Task<ApiResult<V0Dtos.DeviceSearchResponseDto>> SearchDevices(V0Dtos.DeviceSearchRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V0.DevicesEndpoint}/{{deviceId}}/alias", "PATCH")]
  Task<ApiResult<V0Dtos.DeviceResponseDto>> UpdateDeviceAlias(V0Dtos.UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken = default);
}
