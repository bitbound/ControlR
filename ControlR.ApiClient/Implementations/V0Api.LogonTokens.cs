using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V0;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using V0Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient;

internal partial class V0Api
{
  async Task<ApiResult<V0Dtos.LogonTokenResponseDto>> ILogonTokensApi.CreateLogonTokenForExternal(V0Dtos.CreateLogonTokenForExternalRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.V0.LogonTokensEndpoint}/external", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<V0Dtos.LogonTokenResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<V0Dtos.LogonTokenResponseDto>> ILogonTokensApi.CreateLogonTokenForUser(V0Dtos.CreateLogonTokenForUserRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.V0.LogonTokensEndpoint}/user", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<V0Dtos.LogonTokenResponseDto>(cancellationToken);
    });
  }
}
