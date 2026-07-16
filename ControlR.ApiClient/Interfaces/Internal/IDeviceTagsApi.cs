using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IDeviceTagsApi
{
  [ApiRoute($"{HttpConstants.Internal.DeviceTagsEndpoint}", "POST")]
  Task<ApiResult> AddDeviceTag(DeviceTagAddRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.DeviceTagsEndpoint}/{{deviceId}}/{{tagId}}", "DELETE")]
  Task<ApiResult> RemoveDeviceTag(Guid deviceId, Guid tagId, CancellationToken cancellationToken = default);
}
