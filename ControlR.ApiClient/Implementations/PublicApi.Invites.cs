using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Public;

namespace ControlR.ApiClient;

internal partial class PublicApi
{
  async Task<ApiResult<AcceptInvitationResponseDto>> IPublicInvitesApi.AcceptInvitation(
    AcceptInvitationRequestDto request,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.Public.InvitesEndpoint}/accept", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<AcceptInvitationResponseDto>(cancellationToken);
    });
  }
}