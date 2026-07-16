using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V0;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient;

internal partial class V0Api
{
  async Task<ApiResult<CreateTenantResponseDto>> IV0TenantsApi.CreateTenant(CreateTenantRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.V0.TenantsEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreateTenantResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<GetTenantResponseDto>> IV0TenantsApi.GetTenant(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync($"{HttpConstants.V0.TenantsEndpoint}/{tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<GetTenantResponseDto>(cancellationToken);
    });
  }
}