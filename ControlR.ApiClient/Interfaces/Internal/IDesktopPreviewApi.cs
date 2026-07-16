using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IDesktopPreviewApi
{
  [ApiRoute("GET", "/api/internal/desktop-preview/{deviceId}/{targetProcessId}")]
  Task<ApiResult<byte[]>> GetDesktopPreview(Guid deviceId, int targetProcessId, CancellationToken cancellationToken = default);
}
