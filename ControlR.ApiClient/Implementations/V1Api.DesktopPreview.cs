using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<byte[]>> IDesktopPreviewApi.GetDesktopPreview(
    Guid deviceId,
    Guid tenantId,
    int targetProcessId,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync(
        $"{HttpConstants.V1.DesktopPreviewEndpoint}/{deviceId}/{targetProcessId}?tenantId={tenantId}",
        cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    });
  }
}
