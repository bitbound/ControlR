using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IDesktopPreviewApi
{
  [ApiRoute($"{HttpConstants.Internal.DesktopPreviewEndpoint}/{{deviceId}}/{{targetProcessId}}", "GET")]
  Task<ApiResult<byte[]>> GetDesktopPreview(Guid deviceId, int targetProcessId, CancellationToken cancellationToken = default);
}
