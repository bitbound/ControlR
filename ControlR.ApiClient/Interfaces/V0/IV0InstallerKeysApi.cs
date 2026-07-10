using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using V0Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient.Interfaces.V0;

public interface IV0InstallerKeysApi
{
  Task<ApiResult<CreateInstallerKeyResponseDto>> CreateInstallerKey(V0Dtos.CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken = default);
}
