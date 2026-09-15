using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IInstallerKeysApi
{
  [ApiRoute($"{HttpConstants.Internal.InstallerKeysEndpoint}", "POST")]
  [Obsolete("Use ControlrApi.V1.InstallerKeys.CreateInstallerKey (POST /api/v1/installer-keys), which requires tenantId in the request body. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<CreateInstallerKeyResponseDto>> CreateInstallerKey(CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.InstallerKeysEndpoint}/{{keyId}}", "DELETE")]
  [Obsolete("Use ControlrApi.V1.InstallerKeys.DeleteInstallerKey (DELETE /api/v1/installer-keys/{keyId}?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult> DeleteInstallerKey(Guid keyId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.InstallerKeysEndpoint}", "GET")]
  [Obsolete("Use ControlrApi.V1.InstallerKeys.GetAllInstallerKeys (GET /api/v1/installer-keys?tenantId=), which requires tenantId and returns an Items envelope. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<AgentInstallerKeyDto[]>> GetAllInstallerKeys(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.InstallerKeysEndpoint}/usages/{{keyId}}", "GET")]
  [Obsolete("Use ControlrApi.V1.InstallerKeys.GetInstallerKeyUsages (GET /api/v1/installer-keys/{keyId}/usages?tenantId=), which requires tenantId and returns an Items envelope. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<AgentInstallerKeyUsageDto[]>> GetInstallerKeyUsages(Guid keyId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.InstallerKeysEndpoint}/rename", "PUT")]
  [Obsolete("Use ControlrApi.V1.InstallerKeys.RenameInstallerKey (PUT /api/v1/installer-keys/{keyId}?tenantId=), which takes the key id in the route, tenantId as a query parameter, a body with only friendlyName, and returns 204. This internal route is unversioned and slated for removal.")]
  Task<ApiResult> RenameInstallerKey(RenameInstallerKeyRequestDto dto, CancellationToken cancellationToken = default);
}
