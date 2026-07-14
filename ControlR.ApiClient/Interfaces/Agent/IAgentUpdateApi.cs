using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.ApiClient.Interfaces.Agent;

public interface IAgentUpdateApi
{
  Task<ApiResult<BundleMetadataDto>> GetBundleMetadata(RuntimeId runtime, CancellationToken cancellationToken = default);
}
