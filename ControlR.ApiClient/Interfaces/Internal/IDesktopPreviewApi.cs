using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IDesktopPreviewApi
{
  Task<ApiResult<byte[]>> GetDesktopPreview(Guid deviceId, int targetProcessId, CancellationToken cancellationToken = default);
}
