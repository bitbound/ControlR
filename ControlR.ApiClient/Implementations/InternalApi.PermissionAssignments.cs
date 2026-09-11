using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.Internal;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<int>> IPermissionAssignmentsApi.ApplyPresets(InternalDtos.ApplyPermissionPresetsRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/presets/apply", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<int>(cancellationToken);
    });
  }

  async Task<ApiResult<InternalDtos.PermissionAssignmentDto>> IPermissionAssignmentsApi.Create(InternalDtos.CreatePermissionAssignmentRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.Internal.PermissionAssignmentsEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<InternalDtos.PermissionAssignmentDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IPermissionAssignmentsApi.CreateMany(InternalDtos.CreateManyPermissionAssignmentsRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/create-many", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IPermissionAssignmentsApi.Delete(Guid assignmentId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/{assignmentId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<InternalDtos.DeleteManyPermissionAssignmentsResponseDto>> IPermissionAssignmentsApi.DeleteMany(InternalDtos.DeleteManyPermissionAssignmentsRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/delete-many", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<InternalDtos.DeleteManyPermissionAssignmentsResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<InternalDtos.PermissionAssignmentDto[]>> IPermissionAssignmentsApi.GetByPrincipal(string principalKind, Guid principalId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<InternalDtos.PermissionAssignmentDto[]>(
        $"{HttpConstants.Internal.PermissionAssignmentsEndpoint}?principalKind={principalKind}&principalId={principalId}", cancellationToken));
  }

  async Task<ApiResult<InternalDtos.PermissionCatalogEntryDto[]>> IPermissionAssignmentsApi.GetCatalog(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<InternalDtos.PermissionCatalogEntryDto[]>(
        $"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/catalog", cancellationToken));
  }

  async Task<ApiResult<InternalDtos.PermissionPresetDto[]>> IPermissionAssignmentsApi.GetPresets(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<InternalDtos.PermissionPresetDto[]>(
        $"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/presets", cancellationToken));
  }
  async Task<ApiResult> IPermissionAssignmentsApi.Replace(InternalDtos.ReplacePermissionAssignmentsRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/replace", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<InternalDtos.PermissionAssignmentDto>> IPermissionAssignmentsApi.Update(Guid assignmentId, InternalDtos.UpdatePermissionAssignmentRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/{assignmentId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<InternalDtos.PermissionAssignmentDto>(cancellationToken);
    });
  }
}
