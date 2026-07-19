using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using V1Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IInstallerKeysApi
{
  [ApiRoute($"{HttpConstants.V1.InstallerKeysEndpoint}", "POST")]
  Task<ApiResult<V1Dtos.CreateInstallerKeyResponseDto>> CreateInstallerKey(V1Dtos.CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken = default);
}
