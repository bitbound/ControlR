using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IDeviceTagsApi
{
  [ApiRoute("POST", "/api/internal/device-tags")]
  Task<ApiResult> AddDeviceTag(DeviceTagAddRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("DELETE", "/api/internal/device-tags/{deviceId}/{tagId}")]
  Task<ApiResult> RemoveDeviceTag(Guid deviceId, Guid tagId, CancellationToken cancellationToken = default);
}
