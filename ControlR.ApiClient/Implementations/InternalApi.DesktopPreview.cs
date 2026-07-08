using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<byte[]>> IDesktopPreviewApi.GetDesktopPreview(Guid deviceId, int targetProcessId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.DesktopPreviewEndpoint}/{deviceId}/{targetProcessId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    });
  }
}
