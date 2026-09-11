using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IPermissionAssignmentsApi
{
  [ApiRoute($"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/presets/apply", "POST")]
  Task<ApiResult<int>> ApplyPresets(InternalDtos.ApplyPermissionPresetsRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.PermissionAssignmentsEndpoint}", "POST")]
  Task<ApiResult<InternalDtos.PermissionAssignmentDto>> Create(InternalDtos.CreatePermissionAssignmentRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/create-many", "POST")]
  Task<ApiResult> CreateMany(InternalDtos.CreateManyPermissionAssignmentsRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/{{assignmentId}}", "DELETE")]
  Task<ApiResult> Delete(Guid assignmentId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/delete-many", "POST")]
  Task<ApiResult<InternalDtos.DeleteManyPermissionAssignmentsResponseDto>> DeleteMany(InternalDtos.DeleteManyPermissionAssignmentsRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.PermissionAssignmentsEndpoint}", "GET")]
  Task<ApiResult<InternalDtos.PermissionAssignmentDto[]>> GetByPrincipal(string principalKind, Guid principalId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/catalog", "GET")]
  Task<ApiResult<InternalDtos.PermissionCatalogEntryDto[]>> GetCatalog(CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/presets", "GET")]
  Task<ApiResult<InternalDtos.PermissionPresetDto[]>> GetPresets(CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/replace", "POST")]
  Task<ApiResult> Replace(InternalDtos.ReplacePermissionAssignmentsRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.PermissionAssignmentsEndpoint}/{{assignmentId}}", "PUT")]
  Task<ApiResult<InternalDtos.PermissionAssignmentDto>> Update(Guid assignmentId, InternalDtos.UpdatePermissionAssignmentRequestDto request, CancellationToken cancellationToken = default);
}
