using ControlR.Libraries.Api.Contracts.Dtos;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IInstallerKeysApi
{
  Task<ApiResult<InternalDtos.CreateInstallerKeyResponseDto>> CreateInstallerKey(InternalDtos.CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken = default);
  Task<ApiResult> DeleteInstallerKey(Guid keyId, CancellationToken cancellationToken = default);
  Task<ApiResult<InternalDtos.AgentInstallerKeyDto[]>> GetAllInstallerKeys(CancellationToken cancellationToken = default);
  Task<ApiResult<InternalDtos.AgentInstallerKeyUsageDto[]>> GetInstallerKeyUsages(Guid keyId, CancellationToken cancellationToken = default);
  Task<ApiResult> RenameInstallerKey(InternalDtos.RenameInstallerKeyRequestDto dto, CancellationToken cancellationToken = default);
}
