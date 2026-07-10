using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IDeviceTagsApi
{
  Task<ApiResult> AddDeviceTag(DeviceTagAddRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> RemoveDeviceTag(Guid deviceId, Guid tagId, CancellationToken cancellationToken = default);
}
