using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.EffectivePermissions;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<EffectivePermissionQueryResponseDto>> IEffectivePermissionsApi.GetEffectivePermission(
    Guid principalId,
    Guid tenantId,
    PermissionPrincipalKind principalKind,
    string permissionName,
    PermissionScopeKind scopeKind,
    Guid? scopeId,
    CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      var query = $"tenantId={Uri.EscapeDataString(tenantId.ToString())}" +
                  $"&principalKind={Uri.EscapeDataString(principalKind.ToString())}" +
                  $"&permissionName={Uri.EscapeDataString(permissionName)}" +
                  $"&scopeKind={Uri.EscapeDataString(scopeKind.ToString())}";

      if (scopeId.HasValue)
      {
        query += $"&scopeId={Uri.EscapeDataString(scopeId.Value.ToString())}";
      }

      using var response = await _client.HttpClient.GetAsync(
        $"{HttpConstants.V1.EffectivePermissionsEndpoint}/{principalId}?{query}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<EffectivePermissionQueryResponseDto>(cancellationToken);
    });
  }
}