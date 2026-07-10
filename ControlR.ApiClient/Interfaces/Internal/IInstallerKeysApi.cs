using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IInstallerKeysApi
{
  Task<ApiResult<CreateInstallerKeyResponseDto>> CreateInstallerKey(InternalDtos.CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken = default);
  Task<ApiResult> DeleteInstallerKey(Guid keyId, CancellationToken cancellationToken = default);
  Task<ApiResult<AgentInstallerKeyDto[]>> GetAllInstallerKeys(CancellationToken cancellationToken = default);
  Task<ApiResult<AgentInstallerKeyUsageDto[]>> GetInstallerKeyUsages(Guid keyId, CancellationToken cancellationToken = default);
  Task<ApiResult> RenameInstallerKey(RenameInstallerKeyRequestDto dto, CancellationToken cancellationToken = default);
}
