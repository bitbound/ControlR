using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.InstallerKeys;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IInstallerKeysApi
{
  [ApiRoute($"{HttpConstants.V1.InstallerKeysEndpoint}", "POST")]
  Task<ApiResult<CreateInstallerKeyResponseDto>> CreateInstallerKey(CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.InstallerKeysEndpoint}/{{keyId}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteInstallerKey(Guid keyId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.InstallerKeysEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<InstallerKeysResponseDto>> GetAllInstallerKeys(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.InstallerKeysEndpoint}/{{keyId}}/usages?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<InstallerKeyUsagesResponseDto>> GetInstallerKeyUsages(Guid keyId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.InstallerKeysEndpoint}/{{keyId}}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult> RenameInstallerKey(Guid keyId, Guid tenantId, RenameInstallerKeyRequestDto request, CancellationToken cancellationToken = default);
}
