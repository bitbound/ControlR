using ControlR.Libraries.Api.Contracts.Dtos;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IVersionApi
{
  Task<ApiResult<Version>> GetCurrentAgentVersion(CancellationToken cancellationToken = default);
  Task<ApiResult<Version>> GetCurrentServerVersion(CancellationToken cancellationToken = default);
  Task<ApiResult<string>> GetReleaseNotes(CancellationToken cancellationToken = default);
}
