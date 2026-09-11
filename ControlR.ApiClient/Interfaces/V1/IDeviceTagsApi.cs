using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using DeviceTagsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceTags;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IDeviceTagsApi
{
  [ApiRoute($"{HttpConstants.V1.DeviceTagsEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult> AddDeviceTag(Guid tenantId, DeviceTagsDtos.DeviceTagAddRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceTagsEndpoint}/{{deviceId}}/{{tagId}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> RemoveDeviceTag(Guid deviceId, Guid tagId, Guid tenantId, CancellationToken cancellationToken = default);
}
