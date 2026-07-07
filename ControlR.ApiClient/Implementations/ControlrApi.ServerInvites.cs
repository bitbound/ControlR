using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<TenantInviteResponseDto>> IServerInvitesApi.Create(ServerTenantInviteRequestDto dto, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.InvitesEndpoint}/server", dto, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TenantInviteResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IServerInvitesApi.Delete(Guid tenantId, Guid inviteId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.InvitesEndpoint}/server/{tenantId}/{inviteId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<TenantInviteResponseDto[]>> IServerInvitesApi.GetAll(Guid tenantId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<TenantInviteResponseDto[]>($"{HttpConstants.InvitesEndpoint}/server/{tenantId}", cancellationToken));
  }
}
