using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<ServerAlertResponseDto>> IServerAlertApi.GetServerAlert(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync(HttpConstants.Internal.ServerAlertEndpoint, cancellationToken);
      if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
      {
        return ServerAlertResponseDto.Empty;
      }
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<ServerAlertResponseDto>(cancellationToken)
        ?? throw new HttpRequestException("The server response was empty.", null, response.StatusCode);
    });
  }

  async Task<ApiResult<ServerAlertResponseDto>> IServerAlertApi.UpdateServerAlert(ServerAlertRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.Internal.ServerAlertEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<ServerAlertResponseDto>(cancellationToken);
    });
  }
}
