using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<AcceptInvitationResponseDto>> IInvitesApi.AcceptInvitation(
    AcceptInvitationRequestDto request,
    CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.InvitesEndpoint}/accept", request, cancellationToken);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<AcceptInvitationResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<TenantInviteResponseDto>> IInvitesApi.CreateTenantInvite(TenantInviteRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.InvitesEndpoint, request, cancellationToken);
      if (response.StatusCode == HttpStatusCode.Conflict)
      {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
          string.IsNullOrWhiteSpace(content) ? "The invitation already exists." : content,
          inner: null,
          HttpStatusCode.Conflict);
      }
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<TenantInviteResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IInvitesApi.DeleteTenantInvite(Guid inviteId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.InvitesEndpoint}/{inviteId}", cancellationToken);
      response.EnsureSuccessStatusCode();
    });
  }

  async Task<ApiResult<TenantInviteResponseDto[]>> IInvitesApi.GetPendingTenantInvites(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<TenantInviteResponseDto[]>(HttpConstants.InvitesEndpoint, cancellationToken));
  }
}
