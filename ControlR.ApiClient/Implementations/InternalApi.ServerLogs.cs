using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<GetAspireUrlResponseDto>> IServerLogsApi.GetAspireUrl(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.ServerLogsEndpoint}/get-aspire-url", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<GetAspireUrlResponseDto>(cancellationToken);
    });
  }
}
