using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IDevicesApi
{
  [ApiRoute($"{HttpConstants.V1.DevicesEndpoint}/{{deviceId}}", "DELETE")]
  Task<ApiResult> DeleteDevice(Guid deviceId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DevicesEndpoint}/delete-many", "POST")]
  Task<ApiResult<DeleteManyDevicesResponseDto>> DeleteManyDevices(DeleteDevicesRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DevicesEndpoint}/{{deviceId}}/desktop-sessions", "GET")]
  Task<ApiResult<DesktopSessionsResponseDto>> GetActiveDesktopSessions(Guid deviceId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DevicesEndpoint}", "GET")]
  IAsyncEnumerable<DeviceResponseDto> GetAllDevices(CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DevicesEndpoint}/{{deviceId}}", "GET")]
  Task<ApiResult<DeviceResponseDto>> GetDevice(Guid deviceId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DevicesEndpoint}/summary", "GET")]
  IAsyncEnumerable<DeviceSummaryDto> GetDeviceSummaries(CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DevicesEndpoint}/search", "POST")]
  Task<ApiResult<DeviceSearchResponseDto>> SearchDevices(DeviceSearchRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DevicesEndpoint}/{{deviceId}}/alias", "PATCH")]
  Task<ApiResult<DeviceResponseDto>> UpdateDeviceAlias(UpdateDeviceAliasRequestDto request, CancellationToken cancellationToken = default);
}
