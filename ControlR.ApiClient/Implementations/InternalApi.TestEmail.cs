using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult> ITestEmailApi.SendTestEmail(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsync(HttpConstants.Internal.TestEmailEndpoint, null, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}
