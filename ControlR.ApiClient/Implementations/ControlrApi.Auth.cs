using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult> IAuthApi.LogOut(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.AuthEndpoint}/logout", new { }, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}
