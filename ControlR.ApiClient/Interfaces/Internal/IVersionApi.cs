using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IVersionApi
{
  [ApiRoute($"{HttpConstants.Internal.VersionEndpoint}/agent", "GET")]
  Task<ApiResult<Version>> GetCurrentAgentVersion(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.VersionEndpoint}/server", "GET")]
  Task<ApiResult<Version>> GetCurrentServerVersion(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.VersionEndpoint}/release-notes", "GET")]
  Task<ApiResult<string>> GetReleaseNotes(CancellationToken cancellationToken = default);
}
