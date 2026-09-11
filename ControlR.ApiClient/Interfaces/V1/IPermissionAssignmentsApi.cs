using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Enums;
using PADtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IPermissionAssignmentsApi
{
  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}/presets/apply?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<int>> ApplyPresets(Guid tenantId, PADtos.ApplyPermissionPresetsRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<PADtos.PermissionAssignmentDto>> Create(Guid tenantId, PADtos.CreatePermissionAssignmentRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}/batch?tenantId={{tenantId}}", "POST")]
  Task<ApiResult> CreateMany(Guid tenantId, PADtos.CreateManyPermissionAssignmentsRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}/{{assignmentId}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> Delete(Guid assignmentId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}/batch-delete?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<PADtos.DeleteManyPermissionAssignmentsResponseDto>> DeleteMany(Guid tenantId, PADtos.DeleteManyPermissionAssignmentsRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}?tenantId={{tenantId}}&principalKind={{principalKind}}&principalId={{principalId}}", "GET")]
  Task<ApiResult<PADtos.PermissionAssignmentsResponseDto>> GetByPrincipal(Guid tenantId, PermissionPrincipalKind principalKind, Guid principalId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}/catalog?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<PADtos.PermissionCatalogResponseDto>> GetCatalog(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}/presets?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<PADtos.PermissionPresetsResponseDto>> GetPresets(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}/replace?tenantId={{tenantId}}", "POST")]
  Task<ApiResult> Replace(Guid tenantId, PADtos.ReplacePermissionAssignmentsRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PermissionAssignmentsEndpoint}/{{assignmentId}}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult<PADtos.PermissionAssignmentDto>> Update(Guid assignmentId, Guid tenantId, PADtos.UpdatePermissionAssignmentRequestDto request, CancellationToken cancellationToken = default);
}