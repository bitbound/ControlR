using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<TenantInviteResponseDto>> IInvitesApi.CreateTenantInvite(TenantInviteRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.Internal.InvitesEndpoint, request, cancellationToken);
      if (response.StatusCode == HttpStatusCode.Conflict)
      {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
          string.IsNullOrWhiteSpace(content) ? "The invitation already exists." : content,
          inner: null,
          HttpStatusCode.Conflict);
      }
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TenantInviteResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IInvitesApi.DeleteTenantInvite(Guid inviteId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync($"{HttpConstants.Internal.InvitesEndpoint}/{inviteId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<TenantInviteResponseDto[]>> IInvitesApi.GetPendingTenantInvites(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<TenantInviteResponseDto[]>(HttpConstants.Internal.InvitesEndpoint, cancellationToken));
  }
}