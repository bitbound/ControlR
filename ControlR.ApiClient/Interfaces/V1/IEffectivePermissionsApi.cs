using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Enums;
using EPDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.EffectivePermissions;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IEffectivePermissionsApi
{
  [ApiRoute($"{HttpConstants.V1.EffectivePermissionsEndpoint}/{{principalId}}?tenantId={{tenantId}}&principalKind={{principalKind}}&permissionName={{permissionName}}&scopeKind={{scopeKind}}&scopeId={{scopeId}}", "GET")]
  Task<ApiResult<EPDtos.EffectivePermissionQueryResponseDto>> GetEffectivePermission(
    Guid principalId,
    Guid tenantId,
    PermissionPrincipalKind principalKind,
    string permissionName,
    PermissionScopeKind scopeKind,
    Guid? scopeId,
    CancellationToken cancellationToken = default);
}