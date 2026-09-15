using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IVersionApi
{
  [ApiRoute($"{HttpConstants.V1.VersionEndpoint}/agent", "GET")]
  Task<ApiResult<Version>> GetCurrentAgentVersion(CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.VersionEndpoint}/server", "GET")]
  Task<ApiResult<Version>> GetCurrentServerVersion(CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.VersionEndpoint}/release-notes", "GET")]
  Task<ApiResult<string>> GetReleaseNotes(CancellationToken cancellationToken = default);
}
