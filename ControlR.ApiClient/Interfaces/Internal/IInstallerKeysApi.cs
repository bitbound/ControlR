using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IInstallerKeysApi
{
  [ApiRoute($"{HttpConstants.Internal.InstallerKeysEndpoint}", "POST")]
  Task<ApiResult<CreateInstallerKeyResponseDto>> CreateInstallerKey(CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.InstallerKeysEndpoint}/{{keyId}}", "DELETE")]
  Task<ApiResult> DeleteInstallerKey(Guid keyId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.InstallerKeysEndpoint}", "GET")]
  Task<ApiResult<AgentInstallerKeyDto[]>> GetAllInstallerKeys(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.InstallerKeysEndpoint}/usages/{{keyId}}", "GET")]
  Task<ApiResult<AgentInstallerKeyUsageDto[]>> GetInstallerKeyUsages(Guid keyId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.InstallerKeysEndpoint}/rename", "PUT")]
  Task<ApiResult> RenameInstallerKey(RenameInstallerKeyRequestDto dto, CancellationToken cancellationToken = default);
}
