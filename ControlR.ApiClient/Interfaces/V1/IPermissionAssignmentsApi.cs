using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IPermissionAssignmentsApi
{
  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}/presets/apply?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<int>> ApplyPresets(Guid tenantId, ApplyPermissionPresetsRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<PermissionAssignmentDto>> Create(Guid tenantId, CreatePermissionAssignmentRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}/batch?tenantId={{tenantId}}", "POST")]
  Task<ApiResult> CreateMany(Guid tenantId, CreateManyPermissionAssignmentsRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}/{{assignmentId}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> Delete(Guid assignmentId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}/batch-delete?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<DeleteManyPermissionAssignmentsResponseDto>> DeleteMany(Guid tenantId, DeleteManyPermissionAssignmentsRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}?tenantId={{tenantId}}&principalKind={{principalKind}}&principalId={{principalId}}", "GET")]
  Task<ApiResult<PermissionAssignmentsResponseDto>> GetByPrincipal(Guid tenantId, PermissionPrincipalKind principalKind, Guid principalId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}/catalog?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<PermissionCatalogResponseDto>> GetCatalog(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}/presets?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<PermissionPresetsResponseDto>> GetPresets(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}/replace?tenantId={{tenantId}}", "POST")]
  Task<ApiResult> Replace(Guid tenantId, ReplacePermissionAssignmentsRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}/{{assignmentId}}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult<PermissionAssignmentDto>> Update(Guid assignmentId, Guid tenantId, UpdatePermissionAssignmentRequestDto request, CancellationToken cancellationToken = default);
}