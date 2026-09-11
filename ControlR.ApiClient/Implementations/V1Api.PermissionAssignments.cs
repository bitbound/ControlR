using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Enums;
using PADtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult<int>> IPermissionAssignmentsApi.ApplyPresets(Guid tenantId, PADtos.ApplyPermissionPresetsRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}/presets/apply?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<int>(cancellationToken);
    });
  }

  async Task<ApiResult<PADtos.PermissionAssignmentDto>> IPermissionAssignmentsApi.Create(Guid tenantId, PADtos.CreatePermissionAssignmentRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<PADtos.PermissionAssignmentDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IPermissionAssignmentsApi.CreateMany(Guid tenantId, PADtos.CreateManyPermissionAssignmentsRequestDto request, CancellationToken cancellationToken)
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

  async Task<ApiResult<PADtos.DeleteManyPermissionAssignmentsResponseDto>> IPermissionAssignmentsApi.DeleteMany(Guid tenantId, PADtos.DeleteManyPermissionAssignmentsRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}/batch-delete?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<PADtos.DeleteManyPermissionAssignmentsResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<PADtos.PermissionAssignmentsResponseDto>> IPermissionAssignmentsApi.GetByPrincipal(Guid tenantId, PermissionPrincipalKind principalKind, Guid principalId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<PADtos.PermissionAssignmentsResponseDto>(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}?tenantId={tenantId}&principalKind={principalKind}&principalId={principalId}", cancellationToken));
  }

  async Task<ApiResult<PADtos.PermissionCatalogResponseDto>> IPermissionAssignmentsApi.GetCatalog(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<PADtos.PermissionCatalogResponseDto>(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}/catalog?tenantId={tenantId}", cancellationToken));
  }

  async Task<ApiResult<PADtos.PermissionPresetsResponseDto>> IPermissionAssignmentsApi.GetPresets(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<PADtos.PermissionPresetsResponseDto>(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}/presets?tenantId={tenantId}", cancellationToken));
  }

  async Task<ApiResult> IPermissionAssignmentsApi.Replace(Guid tenantId, PADtos.ReplacePermissionAssignmentsRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}/replace?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<PADtos.PermissionAssignmentDto>> IPermissionAssignmentsApi.Update(Guid assignmentId, Guid tenantId, PADtos.UpdatePermissionAssignmentRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.V1.PermissionAssignmentsEndpoint}/{assignmentId}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<PADtos.PermissionAssignmentDto>(cancellationToken);
    });
  }
}