using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult> ITestEmailApi.SendTestEmail(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsync(HttpConstants.TestEmailEndpoint, null, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}
