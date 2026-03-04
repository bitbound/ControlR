using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<byte[]>> IDesktopPreviewApi.GetDesktopPreview(Guid deviceId, int targetProcessId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.GetAsync($"{HttpConstants.DesktopPreviewEndpoint}/{deviceId}/{targetProcessId}", cancellationToken);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    });
  }
}
