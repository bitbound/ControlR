using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using V1Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<V1Dtos.LogonTokenResponseDto>> ILogonTokensApi.CreateLogonTokenForExternal(V1Dtos.CreateLogonTokenForExternalRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.V1.LogonTokensEndpoint}/external", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<V1Dtos.LogonTokenResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<V1Dtos.LogonTokenResponseDto>> ILogonTokensApi.CreateLogonTokenForUser(V1Dtos.CreateLogonTokenForUserRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.V1.LogonTokensEndpoint}/user", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<V1Dtos.LogonTokenResponseDto>(cancellationToken);
    });
  }
}
