using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IVersionApi
{
  [ApiRoute($"{HttpConstants.Internal.VersionEndpoint}/agent", "GET")]
  [Obsolete("Use ControlrApi.V1.Version.GetCurrentAgentVersion (GET /api/v1/version/agent), which returns the same value. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<Version>> GetCurrentAgentVersion(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.VersionEndpoint}/server", "GET")]
  [Obsolete("Use ControlrApi.V1.Version.GetCurrentServerVersion (GET /api/v1/version/server), which returns the same value. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<Version>> GetCurrentServerVersion(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.VersionEndpoint}/release-notes", "GET")]
  Task<ApiResult<string>> GetReleaseNotes(CancellationToken cancellationToken = default);
}
