using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServerLogs;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<GetAspireUrlResponseDto>> IServerLogsApi.GetAspireUrl(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync(
        $"{HttpConstants.V1.ServerLogsEndpoint}/get-aspire-url",
        cancellationToken);

      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<GetAspireUrlResponseDto>(cancellationToken);
    });
  }
}
