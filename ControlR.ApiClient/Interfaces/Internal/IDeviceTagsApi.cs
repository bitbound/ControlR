using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IDeviceTagsApi
{
  [ApiRoute($"{HttpConstants.Internal.DeviceTagsEndpoint}", "POST")]
  [Obsolete("Use ControlrApi.V1.DeviceTags.AddDeviceTag (POST /api/v1/device-tags?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult> AddDeviceTag(DeviceTagAddRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DeviceTagsEndpoint}/{{deviceId}}/{{tagId}}", "DELETE")]
  [Obsolete("Use ControlrApi.V1.DeviceTags.RemoveDeviceTag (DELETE /api/v1/device-tags/{deviceId}/{tagId}?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult> RemoveDeviceTag(Guid deviceId, Guid tagId, CancellationToken cancellationToken = default);
}
