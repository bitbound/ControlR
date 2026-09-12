using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Invites;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<InviteResponseDto>> IInvitesApi.CreateInvite(Guid tenantId, CreateInviteRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.InvitesEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<InviteResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IInvitesApi.DeleteInvite(Guid inviteId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.InvitesEndpoint}/{inviteId}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<InvitesResponseDto>> IInvitesApi.GetInvites(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<InvitesResponseDto>(
        $"{HttpConstants.V1.InvitesEndpoint}?tenantId={tenantId}",
        cancellationToken));
  }
}
