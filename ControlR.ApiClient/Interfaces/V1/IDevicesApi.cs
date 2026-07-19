using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using V1Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IDevicesApi
{
  [ApiRoute($"{HttpConstants.V1.DevicesEndpoint}/{{deviceId}}", "DELETE")]
  Task<ApiResult> DeleteDevice(Guid deviceId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DevicesEndpoint}/delete-many", "POST")]
  Task<ApiResult<V1Dtos.DeleteManyDevicesResponseDto>> DeleteManyDevices(V1Dtos.DeleteDevicesRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DevicesEndpoint}/{{deviceId}}/desktop-sessions", "GET")]
  Task<ApiResult<List<V1Dtos.DesktopSessionResponseDto>>> GetActiveDesktopSessions(Guid deviceId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DevicesEndpoint}", "GET")]
  IAsyncEnumerable<V1Dtos.DeviceResponseDto> GetAllDevices(CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DevicesEndpoint}/{{deviceId}}", "GET")]
  Task<ApiResult<V1Dtos.DeviceResponseDto>> GetDevice(Guid deviceId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DevicesEndpoint}/summary", "GET")]
  IAsyncEnumerable<V1Dtos.DeviceSummaryDto> GetDeviceSummaries(CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DevicesEndpoint}/search", "POST")]
  Task<ApiResult<V1Dtos.DeviceSearchResponseDto>> SearchDevices(V1Dtos.DeviceSearchRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DevicesEndpoint}/{{deviceId}}/alias", "PATCH")]
  Task<ApiResult<V1Dtos.DeviceResponseDto>> UpdateDeviceAlias(V1Dtos.UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken = default);
}
