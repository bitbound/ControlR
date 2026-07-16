using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IVersionApi
{
  [ApiRoute("GET", "/api/internal/version/agent")]
  Task<ApiResult<Version>> GetCurrentAgentVersion(CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/version/server")]
  Task<ApiResult<Version>> GetCurrentServerVersion(CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/version/release-notes")]
  Task<ApiResult<string>> GetReleaseNotes(CancellationToken cancellationToken = default);
}
