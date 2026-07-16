using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IInstallerKeysApi
{
  [ApiRoute("POST", "/api/internal/installer-keys")]
  Task<ApiResult<InternalDtos.CreateInstallerKeyResponseDto>> CreateInstallerKey(InternalDtos.CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken = default);
  [ApiRoute("DELETE", "/api/internal/installer-keys/{keyId}")]
  Task<ApiResult> DeleteInstallerKey(Guid keyId, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/installer-keys")]
  Task<ApiResult<InternalDtos.AgentInstallerKeyDto[]>> GetAllInstallerKeys(CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/installer-keys/usages/{keyId}")]
  Task<ApiResult<InternalDtos.AgentInstallerKeyUsageDto[]>> GetInstallerKeyUsages(Guid keyId, CancellationToken cancellationToken = default);
  [ApiRoute("PUT", "/api/internal/installer-keys/rename")]
  Task<ApiResult> RenameInstallerKey(InternalDtos.RenameInstallerKeyRequestDto dto, CancellationToken cancellationToken = default);
}
