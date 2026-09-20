using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IDesktopPreviewApi
{
  /// <summary>
  /// Captures one frame of a process's desktop on the device named by the route. The answer is a single
  /// image, so its length is whatever arrives.
  /// </summary>
  [ApiRoute($"{HttpConstants.V1.DesktopPreviewEndpoint}/{{deviceId}}/{{targetProcessId}}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<byte[]>> GetDesktopPreview(
    Guid deviceId,
    Guid tenantId,
    int targetProcessId,
    CancellationToken cancellationToken = default);
}
