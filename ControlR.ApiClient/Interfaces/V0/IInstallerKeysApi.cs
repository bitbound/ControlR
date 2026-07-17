using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using V0Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient.Interfaces.V0;

public interface IInstallerKeysApi
{
  [ApiRoute($"{HttpConstants.V0.InstallerKeysEndpoint}", "POST")]
  Task<ApiResult<V0Dtos.CreateInstallerKeyResponseDto>> CreateInstallerKey(V0Dtos.CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken = default);
}
