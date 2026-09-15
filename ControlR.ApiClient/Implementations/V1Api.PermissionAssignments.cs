using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<int>> IPermissionAssignmentsApi.ApplyPresets(Guid tenantId, ApplyPermissionPresetsRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}/presets/apply?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<int>(cancellationToken);
    });
  }

  async Task<ApiResult<PermissionAssignmentDto>> IPermissionAssignmentsApi.Create(Guid tenantId, CreatePermissionAssignmentRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<PermissionAssignmentDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IPermissionAssignmentsApi.CreateMany(Guid tenantId, CreateManyPermissionAssignmentsRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}/batch?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IPermissionAssignmentsApi.Delete(Guid assignmentId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}/{assignmentId}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<DeleteManyPermissionAssignmentsResponseDto>> IPermissionAssignmentsApi.DeleteMany(Guid tenantId, DeleteManyPermissionAssignmentsRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}/batch-delete?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<DeleteManyPermissionAssignmentsResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<PermissionAssignmentsResponseDto>> IPermissionAssignmentsApi.GetByPrincipal(Guid tenantId, PermissionPrincipalKind principalKind, Guid principalId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<PermissionAssignmentsResponseDto>(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}?tenantId={tenantId}&principalKind={principalKind}&principalId={principalId}", cancellationToken));
  }

  async Task<ApiResult<PermissionCatalogResponseDto>> IPermissionAssignmentsApi.GetCatalog(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<PermissionCatalogResponseDto>(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}/catalog?tenantId={tenantId}", cancellationToken));
  }

  async Task<ApiResult<PermissionPresetsResponseDto>> IPermissionAssignmentsApi.GetPresets(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<PermissionPresetsResponseDto>(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}/presets?tenantId={tenantId}", cancellationToken));
  }

  async Task<ApiResult> IPermissionAssignmentsApi.Replace(Guid tenantId, ReplacePermissionAssignmentsRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}/replace?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<PermissionAssignmentDto>> IPermissionAssignmentsApi.Update(Guid assignmentId, Guid tenantId, UpdatePermissionAssignmentRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}/{assignmentId}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<PermissionAssignmentDto>(cancellationToken);
    });
  }
}